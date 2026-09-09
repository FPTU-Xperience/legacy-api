namespace NotificationService.Models;

public sealed class Notification
{
    public int Id { get; set; }
    public int? RecipientUserId { get; set; }
    public string? RecipientRole { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
