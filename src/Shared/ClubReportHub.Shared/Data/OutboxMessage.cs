using System.Text.Json;
using ClubReportHub.Shared.Events;
using Microsoft.EntityFrameworkCore;

namespace ClubReportHub.Shared.Data;

public static class OutboxMessageStatus
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Published = "Published";
    public const string Failed = "Failed";
}

public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string EventTypeName { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? CorrelationId { get; set; }

    public static OutboxMessage FromEvent<TEvent>(TEvent integrationEvent, string routingKey, string? correlationId = null)
        where TEvent : IntegrationEvent
    {
        var eventType = integrationEvent.GetType();
        var resolvedCorrelationId = correlationId ?? integrationEvent.CorrelationId;

        return new OutboxMessage
        {
            Id = integrationEvent.EventId != Guid.Empty ? integrationEvent.EventId : Guid.NewGuid(),
            OccurredAtUtc = integrationEvent.OccurredAtUtc != default ? integrationEvent.OccurredAtUtc : DateTimeOffset.UtcNow,
            EventType = routingKey,
            EventTypeName = eventType.AssemblyQualifiedName ?? eventType.FullName ?? eventType.Name,
            Payload = JsonSerializer.Serialize(integrationEvent, eventType, JsonOptions),
            Status = OutboxMessageStatus.Pending,
            CorrelationId = resolvedCorrelationId
        };
    }
}

public static class OutboxDbContextExtensions
{
    public static OutboxMessage AddOutboxMessage<TEvent>(
        this DbContext db,
        TEvent integrationEvent,
        string routingKey,
        string? correlationId = null)
        where TEvent : IntegrationEvent
    {
        var outboxMessage = OutboxMessage.FromEvent(integrationEvent, routingKey, correlationId);
        db.Set<OutboxMessage>().Add(outboxMessage);
        return outboxMessage;
    }
}

public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder ApplyOutboxConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Status, x.OccurredAtUtc });
            entity.Property(x => x.EventType).HasMaxLength(100);
            entity.Property(x => x.EventTypeName).HasMaxLength(300);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
        });

        return modelBuilder;
    }
}
