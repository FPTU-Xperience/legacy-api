namespace ReportService.Models;

public sealed class AuditLog
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
