namespace ReportService.Models;

public sealed class ReportFeedback
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
    public int ReviewerUserId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
