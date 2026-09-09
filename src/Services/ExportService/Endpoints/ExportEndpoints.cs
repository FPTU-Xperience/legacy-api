using System.Security.Claims;
using System.Text.Json;
using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Messaging;
using ExportService.Contracts;
using ExportService.Data;
using ExportService.Extensions;
using ExportService.Models;
using ExportService.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ExportService.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this WebApplication app)
    {
        var exports = app.MapGroup("/api/exports")
            .WithTags("Exports")
            .RequireAuthorization();

        exports.MapGet("/", ListExports);
        exports.MapGet("/{id:int}", GetExportById);
        exports.MapPost("/", CreateExport);
        exports.MapGet("/{id:int}/download", DownloadExport);
    }

    private static async Task<IResult> ListExports(
        string? status,
        int page,
        int pageSize,
        ExportDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var userId = user.GetUserId();
        bool isGlobalAdmin = user.IsInRole(AuthRoles.Admin) || user.IsInRole(AuthRoles.StudentAffairsAdmin);

        var query = db.ExportRequests.Include(x => x.File).AsNoTracking();
        if (!isGlobalAdmin)
        {
            query = query.Where(x => x.RequestedByUserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            items = rows.Select(ExportExtensions.ToResponse),
            total,
            page,
            pageSize
        });
    }

    private static async Task<IResult> GetExportById(
        int id,
        ExportDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var request = await db.ExportRequests
            .Include(x => x.File)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (request is null) return Results.NotFound();

        var userId = user.GetUserId();
        bool isGlobalAdmin = user.IsInRole(AuthRoles.Admin) || user.IsInRole(AuthRoles.StudentAffairsAdmin);
        if (request.RequestedByUserId != userId && !isGlobalAdmin)
        {
            return Results.Forbid();
        }

        return Results.Ok(ExportExtensions.ToResponse(request));
    }

    private static async Task<IResult> CreateExport(
        CreateExportRequest input,
        ExportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        IEventBus eventBus,
        IBackgroundJobClient backgroundJobs,
        CancellationToken cancellationToken)
    {
        bool isAllowedRole = user.IsInRole(AuthRoles.Admin)
            || user.IsInRole(AuthRoles.StudentAffairsAdmin)
            || user.IsInRole(AuthRoles.ClubManager);

        if (!isAllowedRole)
        {
            return Results.Forbid();
        }

        if (input.ReportId is null or <= 0)
        {
            return Results.BadRequest(new { message = "ReportId is required." });
        }

        var exportType = ExportTypes.Normalize(input.ExportType);
        if (exportType is null)
        {
            return Results.BadRequest(new { message = "ExportType must be PDF, XLSX, or DOCX." });
        }

        var client = httpClientFactory.CreateClient("ReportService");
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            client.DefaultRequestHeaders.Add("Authorization", authHeader.ToString());
        }

        var response = await client.GetAsync($"/api/reports/{input.ReportId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode == System.Net.HttpStatusCode.Forbidden
                ? Results.Forbid()
                : Results.NotFound(new { message = "Báo cáo không tồn tại hoặc không có quyền truy cập." });
        }

        var reportSnapshot = await response.Content.ReadFromJsonAsync<ReportExportSnapshot>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken: cancellationToken);

        if (reportSnapshot is null)
        {
            return Results.BadRequest(new { message = "Dữ liệu báo cáo không hợp lệ." });
        }

        var snapshotJson = JsonSerializer.Serialize(reportSnapshot);

        var request = new ExportRequest
        {
            ExportType = exportType,
            Scope = "Report",
            Status = ExportStatuses.Pending,
            Period = reportSnapshot.Period,
            ClubId = reportSnapshot.ClubId,
            ReportId = reportSnapshot.Id,
            RequestedByUserId = user.GetUserId(),
            RequestedByName = user.GetDisplayName(),
            CriteriaJson = JsonSerializer.Serialize(new { reportId = reportSnapshot.Id, exportType }),
            SnapshotJson = snapshotJson,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        db.ExportRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(
            new ExportRequestedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                request.Id,
                request.ExportType,
                request.Scope,
                request.RequestedByUserId),
            EventRoutingKeys.ExportRequested,
            cancellationToken);

        backgroundJobs.Enqueue<ExportGenerationJob>(
            job => job.GenerateAsync(request.Id, CancellationToken.None));

        return Results.Accepted($"/api/exports/{request.Id}", ExportExtensions.ToResponse(request));
    }

    private static async Task<IResult> DownloadExport(
        int id,
        ExportDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var request = await db.ExportRequests
            .Include(x => x.File)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (request is null)
        {
            return Results.NotFound(new { message = "Tệp xuất không còn tồn tại hoặc không khả dụng." });
        }

        var userId = user.GetUserId();
        bool isGlobalAdmin = user.IsInRole(AuthRoles.Admin) || user.IsInRole(AuthRoles.StudentAffairsAdmin);
        if (request.RequestedByUserId != userId && !isGlobalAdmin)
        {
            return Results.Forbid();
        }

        if (request.File is null)
        {
            return Results.NotFound(new { message = "Tệp xuất không còn tồn tại hoặc không khả dụng." });
        }

        if (request.File.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            if (request.File.IsAvailable)
            {
                request.File.IsAvailable = false;
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.StatusCode(410);
        }

        if (!request.File.IsAvailable || !File.Exists(request.File.FilePath))
        {
            if (request.File.IsAvailable)
            {
                request.File.IsAvailable = false;
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.NotFound(new { message = "Tệp xuất không còn tồn tại hoặc không khả dụng." });
        }

        return Results.File(
            request.File.FilePath,
            request.File.ContentType,
            request.File.FileName,
            enableRangeProcessing: true);
    }
}
