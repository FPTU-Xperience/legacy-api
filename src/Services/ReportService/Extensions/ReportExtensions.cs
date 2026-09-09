using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ReportService.Clients;
using ReportService.Contracts;
using ReportService.Models;
using ReportService.Services;

namespace ReportService.Extensions;

public static class ReportExtensions
{
    public static async Task<ClubAccessSnapshot?> GetAuthorAccessAsync(
        int clubId,
        string reportTag,
        string reportType,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (clubId <= 0)
        {
            return null;
        }

        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        return access.FirstOrDefault(x =>
            x.ClubId == clubId
            && (x.CanManage || (x.CanManageFinance && IsFinancialReport(reportTag, reportType))));
    }

    public static async Task<bool> CanAuthorReportsAsync(
        int clubId,
        string reportTag,
        string reportType,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        return await GetAuthorAccessAsync(clubId, reportTag, reportType, clubAccess, httpContext, cancellationToken) is not null;
    }

    public static bool IsFinancialReport(string? tag, string? reportType)
    {
        static string Normalize(string? value) => (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Replace(' ', '_');

        var normalizedTag = Normalize(tag);
        var normalizedType = Normalize(reportType);
        return normalizedTag is "FINANCE" or "FINANCIAL" or "FINANCIAL_REPORT" or "TÀI_CHÍNH"
            || normalizedType is "FINANCE" or "FINANCIAL" or "FINANCIAL_REPORT" or "TÀI_CHÍNH";
    }

    public static bool IsFutureEventReport(string? reportType) =>
        Services.FutureEventReportRules.IsFutureEvent(reportType);

    public static bool IsFutureEventReportModel(Report report) => IsFutureEventReport(report.ReportType);

    public static string? ValidateFutureEventDetails(
        string reportType,
        IReadOnlyCollection<Contracts.UpsertReportDetailRequest> details)
    {
        var vietnamToday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        return Services.FutureEventReportRules.Validate(
            reportType,
            details.Select(detail => new Services.FutureEventDetailInput(
                detail.ActivityDate,
                detail.ActivityName,
                detail.Description,
                detail.Location)).ToArray(),
            vietnamToday);
    }

    public static async Task<bool> CanManageClubAsync(
        int clubId,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        return access.Any(x => x.ClubId == clubId && x.CanManage);
    }

    public static async Task<bool> CanReadReportAsync(
        Report report,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (IsReportReviewer(user) || report.CreatedByUserId == user.GetUserId())
        {
            return true;
        }

        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        var firstAccess = access.FirstOrDefault(x => x.ClubId == report.ClubId);
        return firstAccess is not null
            && (firstAccess.CanManage
                || (firstAccess.CanManageFinance && IsFutureEventReportModel(report) && report.Status != ReportStatuses.Draft)
                || (firstAccess.CanView && report.Status == ReportStatuses.Approved));
    }

    public static async Task<bool> CanViewFinanceAsync(
        Report report,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!IsFutureEventReportModel(report) || IsReportReviewer(user) || report.CreatedByUserId == user.GetUserId())
        {
            return true;
        }

        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        return access.Any(x => x.ClubId == report.ClubId && (x.CanManage || x.CanManageFinance));
    }

    public static bool IsReportReviewer(ClaimsPrincipal user) =>
        user.IsInRole(AuthRoles.Admin)
        || user.IsInRole(AuthRoles.StudentAffairsAdmin);

    public static string DetermineKpiRating(decimal points) => points switch
    {
        >= 500 => "Excellent",
        >= 200 => "Good",
        >= 50 => "Average",
        _ => "Needs Improvement"
    };

    public static string NormalizeReportTag(string? tag, string? reportType)
    {
        var value = string.IsNullOrWhiteSpace(tag) ? reportType : tag;
        return string.IsNullOrWhiteSpace(value) ? "Activity report" : value.Trim();
    }

    public static string NormalizeReportType(string? reportType, string fallbackTag)
    {
        return string.IsNullOrWhiteSpace(reportType) ? fallbackTag : reportType.Trim();
    }

    public static string NormalizeUploadedReportTag(string? tag, string? reportType)
    {
        var value = string.IsNullOrWhiteSpace(tag) ? reportType : tag;
        return string.IsNullOrWhiteSpace(value) ? "Activity report" : value.Trim();
    }

    public static (bool IsValid, string ErrorMessage, string ContentType) ValidateUploadedReportFile(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return (false, "Vui lòng chọn tệp báo cáo.", string.Empty);
        }

        if (file.Length > 20 * 1024 * 1024)
        {
            return (false, "Dung lượng tệp vượt quá giới hạn cho phép (tối đa 20 MB).", string.Empty);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".pdf" or ".docx" or ".xlsx"))
        {
            return (false, "Định dạng tệp không được hỗ trợ. Chỉ chấp nhận các tệp .pdf, .docx, .xlsx.", string.Empty);
        }

        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

        return (true, string.Empty, contentType);
    }

    public static async Task<(string StoredFileName, string StoragePath, string Checksum, long SizeBytes, string OriginalFileName)> SaveUploadedReportFileAsync(
        IFormFile file,
        int clubId,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var rawName = Path.GetFileName(file.FileName);
        var invalidChars = Path.GetInvalidFileNameChars();
        var originalFileName = string.Concat(rawName.Where(c => !invalidChars.Contains(c))).Trim();
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            originalFileName = "report-file" + Path.GetExtension(file.FileName);
        }

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var safeExt = ext.TrimStart('.');
        var storedFileName = $"uploaded-report-{clubId}-{Guid.NewGuid():N}.{safeExt}";

        var storageDir = Path.GetFullPath(config["Uploads:StoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "report-uploads"));
        Directory.CreateDirectory(storageDir);

        var storagePath = Path.Combine(storageDir, storedFileName);

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        var checksumBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        var checksumHex = Convert.ToHexString(checksumBytes).ToLowerInvariant();

        await File.WriteAllBytesAsync(storagePath, bytes, cancellationToken);

        return (storedFileName, storagePath, checksumHex, file.Length, originalFileName);
    }

    public static ReportDetail ToDetail(Contracts.UpsertReportDetailRequest request, bool includeBudget = true) => new()
    {
        ActivityName = request.ActivityName.Trim(),
        ActivityDate = request.ActivityDate,
        Description = request.Description.Trim(),
        ParticipantCount = Math.Max(0, request.ParticipantCount),
        Outcome = request.Outcome.Trim(),
        ActivityType = request.ActivityType?.Trim(),
        Location = request.Location?.Trim(),
        PartnerUnit = request.PartnerUnit?.Trim(),
        Objective = request.Objective?.Trim(),
        TargetParticipantCount = request.TargetParticipantCount.HasValue ? Math.Max(0, request.TargetParticipantCount.Value) : null,
        BudgetSpent = includeBudget && request.BudgetSpent.HasValue ? Math.Max(0, request.BudgetSpent.Value) : null,
        EvidenceUrl = request.EvidenceUrl?.Trim(),
        SortOrder = request.SortOrder
    };
}
