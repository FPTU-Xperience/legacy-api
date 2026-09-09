namespace ReportService.Models;

public sealed class ReportDetail
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
    public string ActivityName { get; set; } = string.Empty;
    public DateOnly ActivityDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? ActivityType { get; set; }
    public string? Location { get; set; }
    public string? PartnerUnit { get; set; }
    public string? Objective { get; set; }
    public int? TargetParticipantCount { get; set; }
    public decimal? BudgetSpent { get; set; }
    public string? EvidenceUrl { get; set; }
    public int SortOrder { get; set; } = 0;
}
