using System.Text.Json;
using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ClubReportHub.Shared.Messaging;

/// <summary>
/// Redis Streams implementation of <see cref="IEventBus"/>.
/// Uses XADD to append events to a stream and stores the routing key as a stream field.
/// </summary>
public sealed class RedisStreamEventBus(
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RedisStreamOptions> options,
    ILogger<RedisStreamEventBus> logger,
    IHttpContextAccessor? httpContextAccessor = null) : IEventBus
{
    private readonly RedisStreamOptions _options = options.Value;
    private readonly IDatabase _db = connectionMultiplexer.GetDatabase();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, string routingKey, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var retryDelay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMs);
        var attempt = 0;

        while (true)
        {
            attempt++;
            try
            {
                var payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonOptions);

                var valuesList = new List<NameValueEntry>
                {
                    new("eventId", integrationEvent.EventId.ToString()),
                    new("eventType", routingKey),
                    new("occurredAtUtc", integrationEvent.OccurredAtUtc.ToString("O")),
                    new("schemaVersion", "1.0"),
                    new("payload", payload)
                };

                var correlationId = integrationEvent.CorrelationId
                    ?? httpContextAccessor?.HttpContext?.Items[CorrelationIdConstants.ItemKey]?.ToString()
                    ?? httpContextAccessor?.HttpContext?.Request?.Headers[CorrelationIdConstants.HeaderName].FirstOrDefault();

                if (!string.IsNullOrEmpty(correlationId))
                {
                    valuesList.Add(new("correlationId", correlationId));
                }

                // Polymorphically extract correlation metadata from ICorrelatedEvent (OCP compliant)
                if (integrationEvent is ICorrelatedEvent correlated)
                {
                    if (!string.IsNullOrEmpty(correlated.EntityId))
                    {
                        valuesList.Add(new("entityId", correlated.EntityId));
                    }
                    if (!string.IsNullOrEmpty(correlated.ClubId))
                    {
                        valuesList.Add(new("clubId", correlated.ClubId));
                    }
                    if (!string.IsNullOrEmpty(correlated.Period))
                    {
                        valuesList.Add(new("period", correlated.Period));
                    }
                }

                var values = valuesList.ToArray();

                var redisEntryId = await _db.StreamAddAsync(
                    _options.StreamName,
                    values,
                    flags: CommandFlags.None);

                logger.LogInformation(
                    "Published integration event {EventId} ({EventType}) to stream '{StreamName}' with RedisEntryId {RedisEntryId}",
                    integrationEvent.EventId,
                    routingKey,
                    _options.StreamName,
                    redisEntryId);

                return;
            }
            catch (RedisException ex)
            {
                if (attempt >= _options.MaxRetries)
                {
                    logger.LogError(
                        ex,
                        "Failed to publish event {EventId} ({EventType}) after {MaxRetries} attempts. Stream: {StreamName}",
                        integrationEvent.EventId,
                        routingKey,
                        _options.MaxRetries,
                        _options.StreamName);
                    throw;
                }

                logger.LogWarning(
                    ex,
                    "Redis error publishing event {EventId} ({EventType}), attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...",
                    integrationEvent.EventId,
                    routingKey,
                    attempt,
                    _options.MaxRetries,
                    retryDelay.TotalMilliseconds);

                await Task.Delay(retryDelay, cancellationToken);
                retryDelay = TimeSpan.FromMilliseconds(Math.Min(retryDelay.TotalMilliseconds * 2, 10000));
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error publishing event {EventId} ({EventType})",
                    integrationEvent.EventId,
                    routingKey);
                throw;
            }
        }
    }
}
