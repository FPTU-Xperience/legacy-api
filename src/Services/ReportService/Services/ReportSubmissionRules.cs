using System;
using System.IO;
using ReportService.Models;

namespace ReportService.Services;

public static class ReportSubmissionRules
{
    public static bool CanUseUploadedReportAuthorAccess(
        Report report,
        int currentUserId,
        bool isApprovedClubMember)
    {
        if (!isApprovedClubMember || report.CreatedByUserId != currentUserId)
        {
            return false;
        }

        return report.ContentSource == ReportContentSources.UploadedFile
            || report.UploadedFile is not null;
    }

    public static (bool IsValid, string? ErrorMessage, bool IsForbidden) ValidateSubmission(
        Report report,
        int currentUserId,
        bool hasAuthorAccess,
        Func<string, bool>? fileExistsCheck = null)
    {
        if (!hasAuthorAccess || report.CreatedByUserId != currentUserId)
        {
            return (false, "Forbidden", true);
        }

        if (report.Status is not (ReportStatuses.Draft or ReportStatuses.Rejected))
        {
            return (false, "Only draft or rejected reports can be submitted.", false);
        }

        var isUploadedReport = report.ContentSource == ReportContentSources.UploadedFile || report.UploadedFile is not null;
        if (isUploadedReport)
        {
            var checkFileExists = fileExistsCheck ?? File.Exists;
            if (report.UploadedFile is null ||
                !report.UploadedFile.IsActive ||
                report.UploadedFile.ReportId != report.Id ||
                !checkFileExists(report.UploadedFile.StoragePath))
            {
                return (false, "An uploaded report file is required before submission.", false);
            }
        }
        else
        {
            if (report.Details.Count == 0)
            {
                return (false, "At least one activity detail is required before submission.", false);
            }
        }

        return (true, null, false);
    }
}
