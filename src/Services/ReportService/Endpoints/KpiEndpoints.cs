using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Tracing;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using ReportService.Clients;
using ReportService.Data;
using ReportService.Extensions;
using ReportService.Models;

// Use types from KpiGrpcService project reference (KpiGrpcService.Protos namespace)
using KpiContract = ReportService.Contracts;
using KpiGrpcClient = KpiGrpcService.Protos.Client;

namespace ReportService.Endpoints;

public static class KpiEndpoints
{
    public static void MapKpiEndpoints(this WebApplication app)
    {
        var kpis = app.MapGroup("/api/kpis")
            .WithTags("KPI")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        kpis.MapGet("/rules", GetKpiRules);
        kpis.MapGet("/leaderboard", GetKpiLeaderboard);
    }

    private static IResult GetKpiRules()
    {
        return Results.Ok(new[]
        {
            new KpiContract.KpiRuleResponse("APPROVED_REPORT", "Approved report", 50, "Each approved period report adds KPI points."),
            new KpiContract.KpiRuleResponse("ACTIVITY", "Reported activity", 5, "Each approved activity detail contributes operational KPI."),
            new KpiContract.KpiRuleResponse("PARTICIPATION", "Participant engagement", 0.1m, "Each participant in approved activities contributes 0.1 point."),
            new KpiContract.KpiRuleResponse("REJECTED_REPORT", "Rejected report penalty", -10, "Rejected reports reduce KPI until revised and approved."),
            new KpiContract.KpiRuleResponse("OVERDUE_REPORT", "Overdue report penalty", -20, "Draft or rejected reports past due date reduce KPI.")
        });
    }

    private static async Task<IResult> GetKpiLeaderboard(
        string? period,
        ReportDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        ClubDirectoryClient clubDirectory,
        KpiGrpcClient.KpiService.KpiServiceClient grpcClient,
        ILogger<Program> logger,
        CancellationToken httpCancellationToken)
    {
        var canViewAllClubs = ReportExtensions.IsReportReviewer(user);
        var visibleClubs = new Dictionary<int, string>();
        var bearerToken = httpContext.GetBearerToken();

        if (canViewAllClubs)
        {
            var directory = await clubDirectory.GetAllAsync(bearerToken, httpCancellationToken);
            foreach (var club in directory)
            {
                visibleClubs[club.Id] = club.Name;
            }
        }
        else
        {
            var access = await clubAccess.GetMyAccessAsync(bearerToken, httpCancellationToken);
            foreach (var club in access.Where(x => x.CanView))
            {
                visibleClubs[club.ClubId] = club.ClubName;
            }

            if (visibleClubs.Count == 0)
            {
                return Results.Ok(new KpiContract.KpiLeaderboardResponse(period, DateTimeOffset.UtcNow, []));
            }
        }

        var vietnamToday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var query = db.Reports.AsNoTracking().Include(x => x.Details).AsQueryable();
        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(x => x.Period == period);
        }

        if (!canViewAllClubs)
        {
            var visibleClubIds = visibleClubs.Keys.ToArray();
            query = query.Where(x => visibleClubIds.Contains(x.ClubId));
        }

        var reportsForKpi = await query.ToListAsync();
        foreach (var report in reportsForKpi)
        {
            visibleClubs.TryAdd(report.ClubId, report.ClubName);
        }

        var reportsByClub = reportsForKpi
            .GroupBy(x => x.ClubId)
            .ToDictionary(x => x.Key, x => x.ToArray());

        var clubMetrics = visibleClubs
            .Select(club =>
            {
                var clubReports = reportsByClub.GetValueOrDefault(club.Key) ?? [];
                var approved = clubReports.Where(x => x.Status == ReportStatuses.Approved).ToArray();
                var rejectedCount = clubReports.Count(x => x.Status == ReportStatuses.Rejected);
                var overdueCount = clubReports.Count(x =>
                    (x.Status == ReportStatuses.Draft || x.Status == ReportStatuses.Rejected)
                    && x.DueDate < vietnamToday);
                var activityCount = approved.Sum(x => x.Details.Count);
                var participants = approved.Sum(x => x.Details.Sum(d => d.ParticipantCount));
                var points = approved.Length * 50m + activityCount * 5m + participants * 0.1m - rejectedCount * 10m - overdueCount * 20m;
                return new
                {
                    ClubId = club.Key,
                    ClubName = club.Value,
                    Points = Math.Max(0, decimal.Round(points, 2)),
                    ApprovedReports = approved.Length,
                    Activities = activityCount,
                    Participants = participants,
                    RejectedReports = rejectedCount,
                    OverdueReports = overdueCount
                };
            })
            .OrderByDescending(x => x.Points)
            .ThenBy(x => x.ClubName)
            .ToArray();

        var correlationId = httpContext.GetCorrelationId() ?? Guid.NewGuid().ToString();

        var tasks = clubMetrics.Select(async metric =>
        {
            try
            {
                using var grpcCts = CancellationTokenSource.CreateLinkedTokenSource(httpCancellationToken);
                grpcCts.CancelAfter(TimeSpan.FromSeconds(5));
                var grpcRequest = new KpiGrpcClient.KpiClubRequest
                {
                    ClubId = metric.ClubId,
                    ClubName = metric.ClubName,
                    Period = period ?? "",
                    ApprovedReports = metric.ApprovedReports,
                    ActivityCount = metric.Activities,
                    ParticipantCount = (int)metric.Participants,
                    RejectedReports = metric.RejectedReports,
                    OverdueReports = metric.OverdueReports,
                    CorrelationId = correlationId
                };

                logger.LogInformation(
                    "REST calling gRPC CalculateClubKpi for ClubId: {ClubId}, CorrelationId: {CorrelationId}",
                    metric.ClubId, correlationId);

                var grpcCallOptions = new CallOptions(deadline: DateTime.UtcNow.AddSeconds(5), cancellationToken: grpcCts.Token);
                var grpcResponse = await grpcClient.CalculateClubKpiAsync(grpcRequest, grpcCallOptions);

                logger.LogInformation(
                    "gRPC response received for ClubId: {ClubId}, Score: {Score}, Rating: {Rating}, CorrelationId: {CorrelationId}",
                    grpcResponse.ClubId, grpcResponse.TotalScore, grpcResponse.Rating, correlationId);

                return new
                {
                    Metric = metric,
                    TotalScore = (decimal)grpcResponse.TotalScore,
                    Rating = grpcResponse.Rating
                };
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex,
                    "gRPC call failed for ClubId: {ClubId}. Falling back to local calculation. CorrelationId: {CorrelationId}, gRPC Status: {GrpcStatus}",
                    metric.ClubId, correlationId, ex.StatusCode);
                return new
                {
                    Metric = metric,
                    TotalScore = metric.Points,
                    Rating = ReportExtensions.DetermineKpiRating(metric.Points)
                };
            }
        });

        var results = await Task.WhenAll(tasks);

        var ranked = results
            .OrderByDescending(x => x.TotalScore)
            .ThenBy(x => x.Metric.ClubName)
            .Select((x, index) => new KpiContract.KpiLeaderboardRow(
                index + 1,
                x.Metric.ClubId,
                x.Metric.ClubName,
                x.TotalScore,
                x.Rating,
                x.Metric.ApprovedReports,
                x.Metric.Activities,
                x.Metric.Participants,
                x.Metric.RejectedReports,
                x.Metric.OverdueReports))
            .ToList();

        return Results.Ok(new KpiContract.KpiLeaderboardResponse(period, DateTimeOffset.UtcNow, ranked));
    }
}
