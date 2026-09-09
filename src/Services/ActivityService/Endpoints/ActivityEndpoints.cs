using System.Security.Claims;
using ActivityService.Contracts;
using ActivityService.Data;
using ActivityService.Infrastructure;
using ActivityService.Models;
using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using static ActivityService.Endpoints.ActivityEndpointHelpers;

namespace ActivityService.Endpoints;

public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(
        this IEndpointRouteBuilder app)
    {
        var activities = app
            .MapGroup("/api/activities")
            .WithTags("Activities")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        // Implemented endpoints
        MapGetActivities(activities);
        MapGetActivityById(activities);

        return app;
    }

    private static void MapGetActivities(
        RouteGroupBuilder activities)
    {
        activities.MapGet("/", async (
            int? clubId,
            string? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ActivityDbContext db,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ClubAccessClient clubAccess,
            CancellationToken cancellationToken) =>
        {
            var query = db.Activities
                .AsNoTracking()
                .AsQueryable();

            if (!CanReviewAllActivities(user))
            {
                var access = await clubAccess.GetMyAccessAsync(
                    httpContext.GetBearerToken(),
                    cancellationToken);

                var visibleClubIds = access
                    .Where(x => x.CanView)
                    .Select(x => x.ClubId)
                    .ToHashSet();

                query = query.Where(
                    x => visibleClubIds.Contains(x.ClubId));
            }

            if (clubId.HasValue)
            {
                query = query.Where(
                    x => x.ClubId == clubId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(
                    x => x.Status == status);
            }

            if (from.HasValue)
            {
                query = query.Where(
                    x => x.StartTimeUtc >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(
                    x => x.StartTimeUtc <= to.Value);
            }

            var rows = await query
                .OrderBy(x => x.StartTimeUtc)
                .Include(x => x.Participants)
                .Include(x => x.Attendances)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return Results.Ok(
                rows.Select(ToResponse));
        });
    }

    private static void MapGetActivityById(
        RouteGroupBuilder activities)
    {
        activities.MapGet("/{id:int}", async (
            int id,
            ActivityDbContext db,
            ClaimsPrincipal user,
            HttpContext httpContext,
            ClubAccessClient clubAccess,
            CancellationToken cancellationToken) =>
        {
            var activity = await db.Activities
                .AsNoTracking()
                .Include(x => x.Participants)
                .Include(x => x.Attendances)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (activity is null)
            {
                return Results.NotFound();
            }

            if (!CanReviewAllActivities(user))
            {
                var access = await clubAccess.GetMyAccessAsync(
                    httpContext.GetBearerToken(),
                    cancellationToken);

                var canView = access.Any(x => x.ClubId == activity.ClubId && x.CanView);
                if (!canView)
                {
                    return Results.Forbid();
                }
            }

            return Results.Ok(ToResponse(activity));
        });
    }
}