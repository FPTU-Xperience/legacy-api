using System.Security.Claims;
using ActivityService.Contracts;
using ActivityService.Infrastructure;
using ActivityService.Services;
using ClubReportHub.Shared.Auth;
using static ActivityService.Endpoints.ActivityEndpointHelpers;

namespace ActivityService.Endpoints;

public static class MemberStatisticsEndpoints
{
    public static IEndpointRouteBuilder MapMemberStatisticsEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/activities/clubs/{clubId:int}/member-statistics",
            async (
                int clubId,
                MemberStatisticsQuery request,
                MemberActivityStatisticsService statistics,
                ClaimsPrincipal user,
                HttpContext httpContext,
                ClubAccessClient clubAccess,
                CancellationToken cancellationToken) =>
            {
                var canManage =
                    await CanManageClubOrReviewAllAsync(
                        clubId,
                        user,
                        clubAccess,
                        httpContext,
                        cancellationToken);

                if (!canManage)
                {
                    return Results.Forbid();
                }

                if (request.Members is null
                    || request.Members.Count > 500
                    || request.Members.Any(x => x.UserId <= 0))
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "Provide between 0 and 500 valid members."
                    });
                }

                var result = await statistics.GetBatchAsync(
                    clubId,
                    request.Members,
                    DateTimeOffset.UtcNow,
                    cancellationToken);

                return Results.Ok(
                    result.Values.OrderBy(x => x.UserId));
            })
            .RequireAuthorization(AuthPolicies.BusinessAccess)
            .WithTags("Member Activity Statistics");

        app.MapPost(
            "/api/activities/clubs/{clubId:int}/member-statistics/detail",
            async (
                int clubId,
                MemberStatisticsDetailQuery request,
                MemberActivityStatisticsService statistics,
                ClaimsPrincipal user,
                HttpContext httpContext,
                ClubAccessClient clubAccess,
                CancellationToken cancellationToken) =>
            {
                var canManage =
                    await CanManageClubOrReviewAllAsync(
                        clubId,
                        user,
                        clubAccess,
                        httpContext,
                        cancellationToken);

                if (!canManage)
                {
                    return Results.Forbid();
                }

                if (request.UserId <= 0
                    || request.Page < 1
                    || request.PageSize is < 1 or > 100)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "The member and pagination values are invalid."
                    });
                }

                var result = await statistics.GetDetailAsync(
                    clubId,
                    request,
                    DateTimeOffset.UtcNow,
                    cancellationToken);

                return Results.Ok(result);
            })
            .RequireAuthorization(AuthPolicies.BusinessAccess)
            .WithTags("Member Activity Statistics");

        return app;
    }
}