namespace ExportService.Contracts;

public sealed record ReportExportSnapshot(
    int Id,
    int ClubId,
    string ClubName,
    string Period,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    string? ExecutiveSummary,
    string? Achievements,
    string? Challenges,
    string? Recommendations,
    string? NextPeriodPlan,
    int TotalActivities,
    int TotalParticipants,
    decimal TotalBudgetSpent,
    List<ReportActivitySnapshot> Details,
    List<ReportAttachmentSnapshot> Attachments,
    List<ReportFeedbackSnapshot> Feedback
);

public sealed record ReportActivitySnapshot(
    int Id,
    int? ActivityId,
    string ActivityName,
    DateOnly? ActivityDate,
    int ParticipantCount,
    decimal? BudgetSpent
);

public sealed record ReportAttachmentSnapshot(
    int Id,
    string FileName,
    string StoragePath
);

public sealed record ReportFeedbackSnapshot(
    int Id,
    string Message,
    string ReviewerName,
    DateTimeOffset CreatedAtUtc
);
