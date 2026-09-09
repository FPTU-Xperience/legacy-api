using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReportService.Models;

public sealed class Report
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public string ClubName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string ReportType { get; set; } = "Activity report";
    public string Tag { get; set; } = "Activity report";
    public string Status { get; set; } = ReportStatuses.Draft;
    public int CreatedByUserId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public int? ReviewedByUserId { get; set; }
    public int Version { get; set; } = 1;
    public string ContentSource { get; set; } = ReportContentSources.StructuredForm;
    public string? ExecutiveSummary { get; set; }
    public string? Achievements { get; set; }
    public string? Challenges { get; set; }
    public string? Recommendations { get; set; }
    public string? NextPeriodPlan { get; set; }

    /// <summary>
    /// Demo/seed identifier. If non-null, this report is managed by the
    /// demo seeder and may be updated during a demo reset.
    /// Never set by user-facing code.
    /// </summary>
    [JsonIgnore]
    public string? SeedKey { get; set; }

    public ReportUploadedFile? UploadedFile { get; set; }
    public int? BudgetProposalId { get; set; }
    public decimal? BudgetRequestedAmount { get; set; }
    public decimal? BudgetApprovedAmount { get; set; }
    public string? BudgetDescription { get; set; }
    public DateTimeOffset? FinanceSubmittedAtUtc { get; set; }
    public int? PublishedActivityId { get; set; }
    public ICollection<ReportDetail> Details { get; set; } = [];
    public ICollection<ReportAttachment> Attachments { get; set; } = [];
    public ICollection<ReportFeedback> Feedback { get; set; } = [];
}
