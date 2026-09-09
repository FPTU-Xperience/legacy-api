using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using ReportService.Clients;
using ReportService.Contracts;
using ReportService.Data;
using ReportService.Extensions;
using ReportService.Models;
using ReportService.Services;

namespace ReportService.Endpoints;

public static class ReportQueryEndpoints
{
    public static void MapReportQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListReports);
        group.MapGet("/summary", GetReportSummary);
        group.MapGet("/aggregate", GetReportAggregate)
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);
        group.MapGet("/{id:int}", GetReport);
    }

    private static async Task<IResult> ListReports(
        int? clubId,
        string? status,
        string? period,
        string? tag,
        int page,
        int pageSize,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var baseQuery = db.Reports.AsNoTracking();

        var reviewer = ReportExtensions.IsReportReviewer(user);
        var financeVisibleClubIds = new HashSet<int>();
        if (!reviewer)
        {
            var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
            var managedClubIds = access.Where(x => x.CanManage).Select(x => x.ClubId).ToHashSet();
            financeVisibleClubIds = access.Where(x => x.CanManage || x.CanManageFinance).Select(x => x.ClubId).ToHashSet();
            var visibleClubIds = access.Where(x => x.CanView).Select(x => x.ClubId).ToHashSet();
            var userId = user.GetUserId();
            baseQuery = baseQuery.Where(x =>
                managedClubIds.Contains(x.ClubId)
                || x.CreatedByUserId == userId
                || (financeVisibleClubIds.Contains(x.ClubId)
                    && x.ReportType == "FUTURE_EVENT"
                    && x.Status != ReportStatuses.Draft)
                || (visibleClubIds.Contains(x.ClubId) && x.Status == ReportStatuses.Approved));
        }

        if (clubId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.ClubId == clubId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            baseQuery = baseQuery.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(period))
        {
            baseQuery = baseQuery.Where(x => x.Period == period);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            baseQuery = baseQuery.Where(x => x.Tag == tag);
        }

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.UploadedFile)
            .Include(x => x.Details)
            .Include(x => x.Attachments)
            .Include(x => x.Feedback)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            total,
            page,
            pageSize,
            items = rows.Select(report => ReportMappers.ToResponse(
                report,
                !ReportExtensions.IsFutureEventReportModel(report) || reviewer || financeVisibleClubIds.Contains(report.ClubId)))
        });
    }

    private static async Task<IResult> GetReportSummary(
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.Reports.AsNoTracking();
        if (!ReportExtensions.IsReportReviewer(user))
        {
            var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
            var managedClubIds = access.Where(x => x.CanManage).Select(x => x.ClubId).ToHashSet();
            var visibleClubIds = access.Where(x => x.CanView).Select(x => x.ClubId).ToHashSet();
            var userId = user.GetUserId();
            query = query.Where(x =>
                managedClubIds.Contains(x.ClubId)
                || x.CreatedByUserId == userId
                || (visibleClubIds.Contains(x.ClubId) && x.Status == ReportStatuses.Approved));
        }

        var summary = await query
            .GroupBy(_ => 1)
            .Select(g => new ReportSummaryResponse(
                g.Count(),
                g.Sum(x => x.Status == ReportStatuses.Draft ? 1 : 0),
                g.Sum(x => (x.Status == ReportStatuses.Submitted || x.Status == ReportStatuses.AwaitingFinance) ? 1 : 0),
                g.Sum(x => x.Status == ReportStatuses.UnderReview ? 1 : 0),
                g.Sum(x => x.Status == ReportStatuses.Approved ? 1 : 0),
                g.Sum(x => x.Status == ReportStatuses.Rejected ? 1 : 0),
                g.Sum(x => ((x.Status == ReportStatuses.Draft || x.Status == ReportStatuses.Rejected) && x.DueDate < today) ? 1 : 0)))
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(summary ?? new ReportSummaryResponse(0, 0, 0, 0, 0, 0, 0));
    }

    private static async Task<IResult> GetReportAggregate(
        string? period,
        ReportDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Reports.AsNoTracking().Where(x => x.Status == ReportStatuses.Approved);
        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(x => x.Period == period);
        }

        var reportStats = await query
            .Select(r => new
            {
                r.ClubId,
                r.ClubName,
                ActivityCount = r.Details.Count,
                ParticipantCount = r.Details.Sum(d => d.ParticipantCount)
            })
            .ToListAsync(cancellationToken);

        var clubs = reportStats
            .GroupBy(x => new { x.ClubId, x.ClubName })
            .Select(group => new ClubAggregationRow(
                group.Key.ClubId,
                group.Key.ClubName,
                group.Count(),
                group.Sum(x => x.ActivityCount),
                group.Sum(x => x.ParticipantCount)))
            .OrderByDescending(x => x.Participants)
            .ToArray();

        return Results.Ok(new AggregationResponse(
            period,
            reportStats.Count,
            reportStats.Sum(x => x.ActivityCount),
            reportStats.Sum(x => x.ParticipantCount),
            clubs));
    }

    private static async Task<IResult> GetReport(
        int id,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .AsNoTracking()
            .Include(x => x.UploadedFile)
            .Include(x => x.Details)
            .Include(x => x.Attachments)
            .Include(x => x.Feedback)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (report is null)
        {
            return Results.NotFound();
        }

        if (!await ReportExtensions.CanReadReportAsync(report, user, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        var includeFinance = await ReportExtensions.CanViewFinanceAsync(report, user, clubAccess, httpContext, cancellationToken);
        return Results.Ok(ReportMappers.ToResponse(report, includeFinance));
    }
}
