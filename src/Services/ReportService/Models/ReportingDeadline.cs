namespace ReportService.Models;

public sealed class ReportingDeadline
{
    public int Id { get; set; }
    public string Period { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public bool IsActive { get; set; } = true;
}
