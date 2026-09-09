using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReportService.Attachments;
using ReportService.Clients;
using ReportService.Contracts;
using ReportService.Data;
using ReportService.Extensions;
using ReportService.Models;
using ReportService.Services;

namespace ReportService.Endpoints;

public static class ReportFileEndpoints
{
    public static void MapReportFileEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/upload", UploadReportFile).DisableAntiforgery();
        group.MapGet("/{reportId:int}/uploaded-file", GetUploadedFile);
        group.MapGet("/{reportId:int}/uploaded-file/preview", GetUploadedFilePreview);
        group.MapGet("/{reportId:int}/uploaded-file/download", DownloadUploadedFile);
        group.MapPut("/{reportId:int}/uploaded-file", ReplaceUploadedFile).DisableAntiforgery();
        group.MapDelete("/{reportId:int}/uploaded-file", DeleteUploadedFile);

        group.MapPost("/{id:int}/attachments", AddAttachmentMetadata);
        group.MapPost("/{id:int}/attachments/upload", UploadAttachment)
            .Accepts<IFormFile>("multipart/form-data")
            .DisableAntiforgery();
        group.MapGet("/{id:int}/attachments/{attachmentId:int}/download", DownloadAttachment);
    }

    private static async Task<IResult> UploadReportFile(
        HttpContext httpContext,
        IConfiguration config,
        ReportDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Request content type must be multipart/form-data." });
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.BadRequest(new { message = "Vui lòng chọn tệp báo cáo để tải lên." });
        }

        if (!int.TryParse(form["clubId"], out var clubId) || clubId <= 0)
        {
            return Results.BadRequest(new { message = "ClubId không hợp lệ." });
        }

        var period = form["period"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(period))
        {
            return Results.BadRequest(new { message = "Kỳ báo cáo là bắt buộc." });
        }

        var reportType = form["reportType"].ToString().Trim();
        var note = form["note"].ToString().Trim();

        var validation = ReportExtensions.ValidateUploadedReportFile(file);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new { message = validation.ErrorMessage });
        }

        var tag = ReportExtensions.NormalizeReportTag(reportType, reportType);
        var normReportType = ReportExtensions.NormalizeReportType(reportType, tag);
        var authorAccess = await ReportExtensions.GetAuthorAccessAsync(
            clubId,
            tag,
            normReportType,
            clubAccess,
            httpContext,
            cancellationToken);

        if (authorAccess is null)
        {
            return Results.Forbid();
        }

        if (await db.Reports.AnyAsync(x => x.ClubId == clubId && x.Period == period && x.Tag == tag, cancellationToken))
        {
            return Results.Conflict(new { message = "A report already exists for this club, period, and tag." });
        }

        var deadline = await db.ReportingDeadlines.FirstOrDefaultAsync(x => x.Period == period, cancellationToken);
        var dueDate = deadline?.DueDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));

        var savedFile = await ReportExtensions.SaveUploadedReportFileAsync(file, clubId, config, cancellationToken);

        var report = new Report
        {
            ClubId = clubId,
            ClubName = authorAccess.ClubName,
            Period = period,
            ReportType = normReportType,
            Tag = tag,
            DueDate = dueDate,
            CreatedByUserId = user.GetUserId(),
            ContentSource = ReportContentSources.UploadedFile,
            Status = ReportStatuses.Draft,
            ExecutiveSummary = string.IsNullOrWhiteSpace(note) ? null : note
        };

        var uploadedFile = new ReportUploadedFile
        {
            OriginalFileName = savedFile.OriginalFileName,
            StoredFileName = savedFile.StoredFileName,
            ContentType = validation.ContentType,
            FileExtension = Path.GetExtension(savedFile.OriginalFileName).ToLowerInvariant(),
            SizeBytes = savedFile.SizeBytes,
            StoragePath = savedFile.StoragePath,
            Checksum = savedFile.Checksum,
            UploadedByUserId = user.GetUserId(),
            UploadedAtUtc = DateTimeOffset.UtcNow,
            IsActive = true,
            Report = report
        };

        await ReportPreviewGenerator.GeneratePreviewAsync(uploadedFile, config, cancellationToken);
        report.UploadedFile = uploadedFile;
        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "Upload", user.GetUserId(), "Report file uploaded as draft.", cancellationToken);

        return Results.Created($"/api/reports/{report.Id}", ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> GetUploadedFile(
        int reportId,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .Include(x => x.UploadedFile)
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);

        if (report is null)
        {
            return Results.NotFound(new { message = "Không tìm thấy báo cáo." });
        }

        if (!await ReportExtensions.CanReadReportAsync(report, user, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        if (report.UploadedFile is null || !report.UploadedFile.IsActive)
        {
            return Results.NotFound(new { message = "Báo cáo này không có file đính kèm hoặc file đã bị xóa." });
        }

        var isDownloadAvailable = File.Exists(report.UploadedFile.StoragePath);
        var isPreviewAvailable = !string.IsNullOrEmpty(report.UploadedFile.PreviewStoragePath) && File.Exists(report.UploadedFile.StoragePath);

        return Results.Ok(new ReportUploadedFileResponse(
            report.UploadedFile.Id,
            report.UploadedFile.OriginalFileName,
            report.UploadedFile.ContentType,
            report.UploadedFile.FileExtension,
            report.UploadedFile.SizeBytes,
            report.UploadedFile.UploadedAtUtc,
            report.UploadedFile.UploadedByUserId,
            isDownloadAvailable,
            report.UploadedFile.PreviewStatus ?? "Available",
            isPreviewAvailable,
            report.UploadedFile.PreviewErrorMessage));
    }

    private static async Task<IResult> GetUploadedFilePreview(
        int reportId,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        IConfiguration config,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .Include(x => x.UploadedFile)
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);

        if (report is null)
        {
            return Results.NotFound(new { message = "Không tìm thấy báo cáo." });
        }

        if (!await ReportExtensions.CanReadReportAsync(report, user, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        if (report.UploadedFile is null || !report.UploadedFile.IsActive || report.UploadedFile.ReportId != report.Id)
        {
            return Results.NotFound(new { message = "Báo cáo này không có file đính kèm." });
        }

        var uploadedFile = report.UploadedFile;

        if (uploadedFile.PreviewStatus is null || uploadedFile.PreviewStatus == "None" || string.IsNullOrEmpty(uploadedFile.PreviewStoragePath) || !File.Exists(uploadedFile.PreviewStoragePath))
        {
            await ReportPreviewGenerator.GeneratePreviewAsync(uploadedFile, config, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        if (uploadedFile.PreviewStatus == "Pending")
        {
            return Results.Conflict(new { message = "Quá trình tạo bản xem trước đang được xử lý." });
        }

        if (uploadedFile.PreviewStatus == "Failed")
        {
            return Results.BadRequest(new { message = uploadedFile.PreviewErrorMessage ?? "Không thể tạo bản xem trước cho file này." });
        }

        if (uploadedFile.PreviewStatus == "Unsupported" || string.IsNullOrEmpty(uploadedFile.PreviewStoragePath) || !File.Exists(uploadedFile.PreviewStoragePath))
        {
            return Results.BadRequest(new { message = "Định dạng file không hỗ trợ xem trước trực tiếp." });
        }

        var previewPath = Path.GetFullPath(uploadedFile.PreviewStoragePath);
        var previewsDir = Path.GetFullPath(config["Uploads:PreviewStoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "report-previews"));
        var uploadsDir = Path.GetFullPath(config["Uploads:StoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "report-uploads"));

        if (!previewPath.StartsWith(previewsDir, StringComparison.OrdinalIgnoreCase) &&
            !previewPath.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Đường dẫn file xem trước không hợp lệ." });
        }

        var contentType = uploadedFile.PreviewContentType ?? "application/pdf";
        var fileName = Path.GetFileName(previewPath);

        httpContext.Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileName}\"");
        return Results.File(previewPath, contentType, enableRangeProcessing: true);
    }

    private static async Task<IResult> DownloadUploadedFile(
        int reportId,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .Include(x => x.UploadedFile)
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);

        if (report is null)
        {
            return Results.NotFound(new { message = "Không tìm thấy báo cáo." });
        }

        if (!await ReportExtensions.CanReadReportAsync(report, user, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        if (report.UploadedFile is null || !report.UploadedFile.IsActive)
        {
            return Results.NotFound(new { message = "Báo cáo này không có file đính kèm." });
        }

        if (!File.Exists(report.UploadedFile.StoragePath))
        {
            return Results.NotFound(new { message = "Tệp tin vật lý không còn tồn tại trên máy chủ." });
        }

        return Results.File(
            report.UploadedFile.StoragePath,
            report.UploadedFile.ContentType,
            report.UploadedFile.OriginalFileName,
            enableRangeProcessing: true);
    }

    private static async Task<IResult> ReplaceUploadedFile(
        int reportId,
        HttpContext httpContext,
        IConfiguration config,
        ReportDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .Include(x => x.UploadedFile)
            .Include(x => x.Details)
            .Include(x => x.Attachments)
            .Include(x => x.Feedback)
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);

        if (report is null)
        {
            return Results.NotFound(new { message = "Không tìm thấy báo cáo." });
        }

        if (!await ReportExtensions.CanAuthorReportsAsync(report.ClubId, report.Tag, report.ReportType, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        if (report.Status is not (ReportStatuses.Draft or ReportStatuses.Rejected))
        {
            return Results.BadRequest(new { message = "Chỉ có thể thay đổi tệp báo cáo khi ở trạng thái Nháp hoặc Thất bại/Yêu cầu sửa." });
        }

        if (!httpContext.Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Request content type must be multipart/form-data." });
        }

        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.BadRequest(new { message = "Vui lòng chọn tệp báo cáo mới." });
        }

        var validation = ReportExtensions.ValidateUploadedReportFile(file);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new { message = validation.ErrorMessage });
        }

        var savedFile = await ReportExtensions.SaveUploadedReportFileAsync(file, report.ClubId, config, cancellationToken);

        if (report.UploadedFile is not null)
        {
            report.UploadedFile.IsActive = false;
        }

        var newUploadedFile = new ReportUploadedFile
        {
            ReportId = report.Id,
            OriginalFileName = savedFile.OriginalFileName,
            StoredFileName = savedFile.StoredFileName,
            ContentType = validation.ContentType,
            FileExtension = Path.GetExtension(savedFile.OriginalFileName).ToLowerInvariant(),
            SizeBytes = savedFile.SizeBytes,
            StoragePath = savedFile.StoragePath,
            Checksum = savedFile.Checksum,
            UploadedByUserId = user.GetUserId(),
            UploadedAtUtc = DateTimeOffset.UtcNow,
            IsActive = true
        };

        report.UploadedFile = newUploadedFile;
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "ReplaceUploadedFile", user.GetUserId(), "Uploaded report file replaced.", cancellationToken);

        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> DeleteUploadedFile(
        int reportId,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .Include(x => x.UploadedFile)
            .Include(x => x.Details)
            .Include(x => x.Attachments)
            .Include(x => x.Feedback)
            .FirstOrDefaultAsync(x => x.Id == reportId, cancellationToken);

        if (report is null)
        {
            return Results.NotFound(new { message = "Không tìm thấy báo cáo." });
        }

        if (!await ReportExtensions.CanAuthorReportsAsync(report.ClubId, report.Tag, report.ReportType, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        if (report.Status is not (ReportStatuses.Draft or ReportStatuses.Rejected))
        {
            return Results.BadRequest(new { message = "Chỉ có thể xóa tệp báo cáo khi ở trạng thái Nháp hoặc Thất bại." });
        }

        if (report.UploadedFile is not null)
        {
            report.UploadedFile.IsActive = false;
            report.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await AuditHelper.AddAuditAsync(db, report.Id, "DeleteUploadedFile", user.GetUserId(), "Uploaded report file deleted.", cancellationToken);
        }

        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> AddAttachmentMetadata(
        int id,
        AddAttachmentRequest request,
        ReportDbContext db,
        IOptions<ReportAttachmentOptions> attachmentOptions,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports.Include(x => x.Details).Include(x => x.Attachments).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.CreatedByUserId != user.GetUserId()
            || !await ReportExtensions.CanAuthorReportsAsync(
                report.ClubId,
                report.Tag,
                report.ReportType,
                clubAccess,
                httpContext,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (report.Status is not (ReportStatuses.Draft or ReportStatuses.Rejected))
        {
            return Results.BadRequest(new { message = "Attachments can only be changed on draft or rejected reports." });
        }

        var validation = ReportAttachmentPolicy.Validate(request.FileName, request.ContentType, request.SizeBytes, attachmentOptions.Value);
        if (!validation.Succeeded)
        {
            return Results.BadRequest(new { message = validation.ErrorMessage });
        }

        if (request.ReportDetailId.HasValue && report.Details.All(x => x.Id != request.ReportDetailId.Value))
        {
            return Results.BadRequest(new { message = "Report detail does not belong to this report." });
        }

        var safeName = ReportAttachmentPolicy.GetSafeFileName(request.FileName);
        report.Attachments.Add(new ReportAttachment
        {
            ReportDetailId = request.ReportDetailId,
            FileName = safeName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            StoragePath = request.StoragePath.Trim()
        });
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "Attachment", user.GetUserId(), $"Attachment metadata added: {safeName}", cancellationToken);
        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> UploadAttachment(
        int id,
        IFormFile? file,
        [FromForm] int? reportDetailId,
        ReportDbContext db,
        IOptions<ReportAttachmentOptions> attachmentOptions,
        IWebHostEnvironment environment,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .Include(x => x.Details)
            .Include(x => x.Attachments)
            .Include(x => x.Feedback)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.CreatedByUserId != user.GetUserId()
            || !await ReportExtensions.CanAuthorReportsAsync(
                report.ClubId,
                report.Tag,
                report.ReportType,
                clubAccess,
                httpContext,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (report.Status is not (ReportStatuses.Draft or ReportStatuses.Rejected))
        {
            return Results.BadRequest(new { message = "Attachments can only be changed on draft or rejected reports." });
        }

        if (reportDetailId.HasValue && report.Details.All(x => x.Id != reportDetailId.Value))
        {
            return Results.BadRequest(new { message = "Report detail does not belong to this report." });
        }

        if (file is null)
        {
            return Results.BadRequest(new { message = "Evidence file is required." });
        }

        var validation = ReportAttachmentPolicy.Validate(file.FileName, file.ContentType, file.Length, attachmentOptions.Value);
        if (!validation.Succeeded)
        {
            return Results.BadRequest(new { message = validation.ErrorMessage });
        }

        var safeName = ReportAttachmentPolicy.GetSafeFileName(file.FileName);
        var storageRoot = ReportAttachmentPolicy.ResolveStorageRoot(attachmentOptions.Value.StoragePath, environment.ContentRootPath);
        var reportFolder = Path.Combine(storageRoot, report.Id.ToString());

        var normalizedFolder = Path.GetFullPath(reportFolder);
        if (!normalizedFolder.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Invalid storage path." });
        }

        Directory.CreateDirectory(reportFolder);

        var storedFileName = ReportAttachmentPolicy.CreateStoredFileName(safeName);
        var filePath = Path.Combine(reportFolder, storedFileName);

        var normalizedFilePath = Path.GetFullPath(filePath);
        if (!normalizedFilePath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Invalid file path." });
        }

        await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        report.Attachments.Add(new ReportAttachment
        {
            ReportDetailId = reportDetailId,
            FileName = safeName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            StoragePath = filePath
        });
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "AttachmentUpload", user.GetUserId(), $"Evidence uploaded: {safeName}", cancellationToken);
        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> DownloadAttachment(
        int id,
        int attachmentId,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (!await ReportExtensions.CanReadReportAsync(report, user, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        var attachment = await db.ReportAttachments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ReportId == id && x.Id == attachmentId, cancellationToken);
        if (attachment is null || !File.Exists(attachment.StoragePath))
        {
            return Results.NotFound(new { message = "Attachment file is not available." });
        }

        return Results.File(attachment.StoragePath, attachment.ContentType, attachment.FileName);
    }
}
