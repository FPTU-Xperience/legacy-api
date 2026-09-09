using ReportService.Contracts;
using ReportService.Extensions;
using ReportService.Models;

namespace ReportService.Extensions;

public static class ReportMappers
{
    public static ReportResponse ToResponse(Report report, bool includeFinance = true)
    {
        var isUploadedFileAvailable = report.UploadedFile is not null
            && report.UploadedFile.IsActive
            && File.Exists(report.UploadedFile.StoragePath);

        return new(
            report.Id,
            report.ClubId,
            report.ClubName,
            report.Period,
            report.ReportType,
            report.Tag,
            report.Status,
            report.CreatedByUserId,
            report.DueDate,
            report.CreatedAtUtc,
            report.UpdatedAtUtc,
            report.SubmittedAtUtc,
            report.ReviewedAtUtc,
            report.Version,
            report.ExecutiveSummary,
            report.Achievements,
            report.Challenges,
            report.Recommendations,
            report.NextPeriodPlan,
            report.Details.Count,
            report.Details.Sum(x => x.ParticipantCount),
            !ReportExtensions.IsFutureEventReportModel(report) || includeFinance ? report.Details.Sum(x => x.BudgetSpent ?? 0m) : 0m,
            includeFinance ? report.BudgetProposalId : null,
            includeFinance ? report.BudgetRequestedAmount : null,
            includeFinance ? report.BudgetApprovedAmount : null,
            includeFinance ? report.BudgetDescription : null,
            includeFinance ? report.FinanceSubmittedAtUtc : null,
            report.PublishedActivityId,
            !ReportExtensions.IsFutureEventReportModel(report) || includeFinance,
            report.Details.OrderBy(x => x.SortOrder).ThenBy(x => x.ActivityDate).Select(x => new ReportDetailResponse(
                x.Id,
                x.ActivityName,
                x.ActivityDate,
                x.Description,
                x.ParticipantCount,
                x.Outcome,
                x.ActivityType,
                x.Location,
                x.PartnerUnit,
                x.Objective,
                x.TargetParticipantCount,
                !ReportExtensions.IsFutureEventReportModel(report) || includeFinance ? x.BudgetSpent : null,
                x.EvidenceUrl,
                x.SortOrder)).ToArray(),
            report.Attachments.OrderByDescending(x => x.UploadedAtUtc).Select(x => new ReportAttachmentResponse(
                x.Id,
                x.ReportDetailId,
                x.FileName,
                x.ContentType,
                x.SizeBytes,
                x.StoragePath,
                x.UploadedAtUtc)).ToArray(),
            report.Feedback.OrderByDescending(x => x.CreatedAtUtc).Select(x => new ReportFeedbackResponse(
                x.Id,
                x.ReviewerUserId,
                x.ReviewerName,
                x.Decision,
                x.Message,
                x.CreatedAtUtc)).ToArray(),
            report.ContentSource ?? ReportContentSources.StructuredForm,
            report.UploadedFile is null || !report.UploadedFile.IsActive
                ? null
                : new ReportUploadedFileResponse(
                    report.UploadedFile.Id,
                    report.UploadedFile.OriginalFileName,
                    report.UploadedFile.ContentType,
                    report.UploadedFile.FileExtension,
                    report.UploadedFile.SizeBytes,
                    report.UploadedFile.UploadedAtUtc,
                    report.UploadedFile.UploadedByUserId,
                    isUploadedFileAvailable,
                    report.UploadedFile.PreviewStatus ?? "Available",
                    !string.IsNullOrEmpty(report.UploadedFile.PreviewStoragePath) && File.Exists(report.UploadedFile.PreviewStoragePath),
                    report.UploadedFile.PreviewErrorMessage));
    }
}
