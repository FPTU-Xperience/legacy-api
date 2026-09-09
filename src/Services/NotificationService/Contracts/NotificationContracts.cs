namespace NotificationService.Contracts;

public sealed record NotificationResponse(
    int Id,
    int? RecipientUserId,
    string? RecipientRole,
    string EventType,
    string Title,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAtUtc);
