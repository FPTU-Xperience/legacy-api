namespace ReportService.Contracts;

public sealed record ReportResponse(
    int Id,
    int ClubId,
    string ClubName,
    string Period,
    string ReportType,
    string Tag,
    string Status,
    int CreatedByUserId,
    DateOnly DueDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    int Version,
    string? ExecutiveSummary,
    string? Achievements,
    string? Challenges,
    string? Recommendations,
    string? NextPeriodPlan,
    int TotalActivities,
    int TotalParticipants,
    decimal TotalBudgetSpent,
    int? BudgetProposalId,
    decimal? BudgetRequestedAmount,
    decimal? BudgetApprovedAmount,
    string? BudgetDescription,
    DateTimeOffset? FinanceSubmittedAtUtc,
    int? PublishedActivityId,
    bool CanViewFinance,
    IReadOnlyCollection<ReportDetailResponse> Details,
    IReadOnlyCollection<ReportAttachmentResponse> Attachments,
    IReadOnlyCollection<ReportFeedbackResponse> Feedback,
    string ContentSource = "STRUCTURED_FORM",
    ReportUploadedFileResponse? UploadedFile = null);

public sealed record ReportUploadedFileResponse(
    int Id,
    string OriginalFileName,
    string ContentType,
    string FileExtension,
    long SizeBytes,
    DateTimeOffset UploadedAtUtc,
    int UploadedByUserId,
    bool IsDownloadAvailable,
    string? PreviewStatus = "Available",
    bool IsPreviewAvailable = true,
    string? PreviewErrorMessage = null);

public sealed record ReportDetailResponse(
    int Id,
    string ActivityName,
    DateOnly ActivityDate,
    string Description,
    int ParticipantCount,
    string Outcome,
    string? ActivityType = null,
    string? Location = null,
    string? PartnerUnit = null,
    string? Objective = null,
    int? TargetParticipantCount = null,
    decimal? BudgetSpent = null,
    string? EvidenceUrl = null,
    int SortOrder = 0);

public sealed record ReportAttachmentResponse(
    int Id,
    int? ReportDetailId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StoragePath,
    DateTimeOffset UploadedAtUtc);

public sealed record ReportFeedbackResponse(
    int Id,
    int ReviewerUserId,
    string ReviewerName,
    string Decision,
    string Message,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateReportRequest(
    int ClubId,
    string Period,
    string? ReportType,
    string? ExecutiveSummary,
    string? Achievements,
    string? Challenges,
    string? Recommendations,
    string? NextPeriodPlan,
    IReadOnlyCollection<UpsertReportDetailRequest> Details,
    string? Tag = null,
    string? ClubName = null,
    DateOnly? DueDate = null);

public sealed record UpdateReportRequest(
    string Period,
    string? ReportType,
    string? ExecutiveSummary,
    string? Achievements,
    string? Challenges,
    string? Recommendations,
    string? NextPeriodPlan,
    IReadOnlyCollection<UpsertReportDetailRequest> Details,
    string? Tag = null,
    DateOnly? DueDate = null);

public sealed record UpsertReportDetailRequest(
    int? Id,
    string ActivityName,
    DateOnly ActivityDate,
    string Description,
    int ParticipantCount,
    string Outcome,
    string? ActivityType = null,
    string? Location = null,
    string? PartnerUnit = null,
    string? Objective = null,
    int? TargetParticipantCount = null,
    decimal? BudgetSpent = null,
    string? EvidenceUrl = null,
    int SortOrder = 0);

public sealed record AddAttachmentRequest(
    int? ReportDetailId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string StoragePath);

public sealed record ReviewRequest(string? Feedback);

public sealed record LinkFutureEventBudgetRequest(
    int BudgetProposalId,
    [System.ComponentModel.DataAnnotations.Range(0.01, 1_000_000_000)] decimal RequestedAmount,
    [System.ComponentModel.DataAnnotations.StringLength(2000)] string Description);

public sealed record DeadlineRequest(string Period, DateOnly DueDate, bool IsActive);

public sealed record MyDeadlineResponse(
    int Id,
    string Period,
    DateOnly DueDate,
    bool IsOverdue,
    int DaysRemaining);

public sealed record ReportSummaryResponse(
    int Total,
    int Draft,
    int Submitted,
    int UnderReview,
    int Approved,
    int Rejected,
    int Overdue);

public sealed record AggregationResponse(
    string? Period,
    int ApprovedReports,
    int TotalActivities,
    int TotalParticipants,
    IReadOnlyCollection<ClubAggregationRow> Clubs);

public sealed record ClubAggregationRow(
    int ClubId,
    string ClubName,
    int ApprovedReports,
    int Activities,
    int Participants);

public sealed record KpiLeaderboardResponse(
    string? Period,
    DateTimeOffset CalculatedAtUtc,
    IReadOnlyCollection<KpiLeaderboardRow> Clubs);

public sealed record KpiLeaderboardRow(
    int Rank,
    int ClubId,
    string ClubName,
    decimal Points,
    string Rating,
    int ApprovedReports,
    int Activities,
    int Participants,
    int RejectedReports,
    int OverdueReports);

public sealed record KpiRuleResponse(
    string Code,
    string Name,
    decimal Points,
    string Description);
