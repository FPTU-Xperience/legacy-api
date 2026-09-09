using System.Text.Json;
using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Messaging;
using ClubReportHub.Shared.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClubReportHub.Shared.Data;

public sealed class OutboxPublisherBackgroundService<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IEventBus eventBus,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherBackgroundService<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Outbox publisher background service started for {DbContextType}.", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessPendingMessagesAsync(stoppingToken);
                if (processedCount == 0)
                {
                    await Task.Delay(_options.PollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in outbox publisher loop for {DbContextType}. Retrying in {DelayMs}ms...",
                    typeof(TDbContext).Name, _options.PollingInterval.TotalMilliseconds);
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
        }

        logger.LogInformation("Outbox publisher background service stopped for {DbContextType}.", typeof(TDbContext).Name);
    }

    private async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var pendingMessages = await db.Set<OutboxMessage>()
            .Where(x => x.Status == OutboxMessageStatus.Pending)
            .OrderBy(x => x.OccurredAtUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        foreach (var message in pendingMessages)
        {
            using var logScope = logger.BeginScope(new Dictionary<string, object>
            {
                [CorrelationIdConstants.ItemKey] = message.CorrelationId ?? message.Id.ToString("N")
            });

            try
            {
                var eventType = ResolveEventType(message.EventTypeName);
                if (eventType == null)
                {
                    throw new InvalidOperationException($"Cannot resolve event CLR type: '{message.EventTypeName}'");
                }

                var integrationEvent = (IntegrationEvent?)JsonSerializer.Deserialize(message.Payload, eventType, JsonOptions);
                if (integrationEvent == null)
                {
                    throw new InvalidOperationException($"Deserialization of payload yielded null for event: '{message.EventTypeName}'");
                }

                if (!string.IsNullOrEmpty(message.CorrelationId) && string.IsNullOrEmpty(integrationEvent.CorrelationId))
                {
                    integrationEvent = integrationEvent with { CorrelationId = message.CorrelationId };
                }

                await eventBus.PublishAsync(integrationEvent, message.EventType, cancellationToken);

                message.Status = OutboxMessageStatus.Published;
                message.ProcessedAtUtc = DateTimeOffset.UtcNow;
                message.ErrorMessage = null;

                logger.LogInformation(
                    "Published outbox event {EventId} ({EventType}) for {DbContextType}.",
                    message.Id, message.EventType, typeof(TDbContext).Name);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.ErrorMessage = ex.Message;

                if (message.RetryCount >= _options.MaxRetries)
                {
                    message.Status = OutboxMessageStatus.Failed;
                    logger.LogError(ex,
                        "Outbox event {EventId} ({EventType}) reached max retries ({MaxRetries}) and is marked Failed.",
                        message.Id, message.EventType, _options.MaxRetries);
                }
                else
                {
                    logger.LogWarning(ex,
                        "Failed to publish outbox event {EventId} ({EventType}), attempt {Attempt}/{MaxRetries}.",
                        message.Id, message.EventType, message.RetryCount, _options.MaxRetries);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return pendingMessages.Count;
    }

    private static Type? ResolveEventType(string eventTypeName)
    {
        var type = Type.GetType(eventTypeName);
        if (type != null)
        {
            return type;
        }

        var sharedAssembly = typeof(IntegrationEvent).Assembly;
        return sharedAssembly.GetType(eventTypeName)
            ?? sharedAssembly.GetExportedTypes().FirstOrDefault(t => t.Name == eventTypeName || t.FullName == eventTypeName);
    }
}
