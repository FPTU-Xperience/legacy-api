namespace NotificationService.Models;

public sealed class ProcessedEvent
{
    public Guid EventId { get; set; }
    public string RoutingKey { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
