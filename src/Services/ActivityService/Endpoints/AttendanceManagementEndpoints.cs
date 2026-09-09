using ActivityService.Contracts;
using ActivityService.Data;
using ActivityService.Infrastructure;
using ActivityService.Models;
using ActivityService.Services;
using ClubReportHub.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;
using static ActivityService.Endpoints.ActivityEndpointHelpers;

namespace ActivityService.Endpoints;

public static class AttendanceManagementEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceManagementEndpoints(
        this IEndpointRouteBuilder app)
    {
        MapGetAttendance(app);
        MapUpdateMemberAttendance(app);
        MapBulkUpdateAttendance(app);

        return app;
    }

    private static void MapGetAttendance(
        IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/clubs/{clubId:int}/activities/{activityId:int}/attendance",
            async (
                int clubId,
                int activityId,
                string? search,
                int page,
                int pageSize,
                ActivityDbContext db,
                ClubMemberRosterClient rosterClient,
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

                var activity = await db.Activities
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == activityId
                            && x.ClubId == clubId,
                        cancellationToken);

                if (activity is null)
                {
                    return Results.NotFound(new
                    {
                        message = "Activity not found in this club."
                    });
                }

                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);

                ClubMemberRosterPage roster;

                try
                {
                    roster = await rosterClient.GetAsync(
                        clubId,
                        activity.StartTimeUtc,
                        search,
                        page,
                        pageSize,
                        httpContext.GetBearerToken(),
                        cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    return Results.Problem(
                        exception.Message,
                        statusCode:
                            StatusCodes.Status503ServiceUnavailable);
                }

                var pageUserIds = roster.Items
                    .Select(x => x.UserId)
                    .ToArray();

                var attendanceRows =
                    await db.ActivityAttendances
                        .AsNoTracking()
                        .Where(x =>
                            x.ActivityId == activityId
                            && pageUserIds.Contains(x.UserId))
                        .OrderByDescending(x =>
                            x.UpdatedAtUtc ?? x.CreatedAtUtc)
                        .ToListAsync(cancellationToken);

                var attendanceByUser = attendanceRows
                    .GroupBy(x => x.UserId)
                    .ToDictionary(
                        x => x.Key,
                        x => x.First());

                var allStatuses =
                    await db.ActivityAttendances
                        .AsNoTracking()
                        .Where(x => x.ActivityId == activityId)
                        .GroupBy(x => x.Status)
                        .Select(x => new
                        {
                            Status = x.Key,
                            Count = x
                                .Select(a => a.UserId)
                                .Distinct()
                                .Count()
                        })
                        .ToDictionaryAsync(
                            x => x.Status,
                            x => x.Count,
                            cancellationToken);

                var items = roster.Items
                    .Select(member =>
                    {
                        attendanceByUser.TryGetValue(
                            member.UserId,
                            out var attendance);

                        return new AttendanceMemberResponse(
                            member.Id,
                            member.UserId,
                            member.FullName,
                            member.Email,
                            member.PhoneNumber,
                            member.Role,
                            member.JoinedAtUtc,
                            attendance?.Status
                                ?? AttendanceStatuses.NotMarked,
                            attendance?.Note,
                            attendance?.CheckedInAtUtc,
                            attendance?.CheckedInByUserId);
                    })
                    .ToArray();

                var present = allStatuses.GetValueOrDefault(
                    AttendanceStatuses.Present);

                var absent = allStatuses.GetValueOrDefault(
                    AttendanceStatuses.Absent);

                var excused = allStatuses.GetValueOrDefault(
                    AttendanceStatuses.Excused);

                var late = allStatuses.GetValueOrDefault(
                    AttendanceStatuses.Late);

                var notMarked = Math.Max(
                    0,
                    roster.TotalItems
                    - present
                    - absent
                    - excused
                    - late);

                return Results.Ok(
                    new ActivityAttendanceManagementResponse(
                        activity.Id,
                        activity.ClubId,
                        activity.Title,
                        activity.StartTimeUtc,
                        activity.Status,
                        items,
                        roster.Page,
                        roster.PageSize,
                        roster.TotalItems,
                        roster.TotalPages,
                        present,
                        absent,
                        excused,
                        late,
                        notMarked));
            })
            .RequireAuthorization(AuthPolicies.BusinessAccess)
            .WithTags("Activity Attendance");
    }

    private static void MapUpdateMemberAttendance(
        IEndpointRouteBuilder app)
    {
        app.MapPut(
            "/api/clubs/{clubId:int}/activities/{activityId:int}/attendance/{memberId:int}",
            async (
                int clubId,
                int activityId,
                int memberId,
                UpdateMemberAttendanceRequest request,
                ActivityDbContext db,
                ClubMemberRosterClient rosterClient,
                ClaimsPrincipal user,
                HttpContext httpContext,
                ClubAccessClient clubAccess,
                CancellationToken cancellationToken) =>
            {
                var validation = ValidateAttendance(
                    request.Status,
                    request.Note);

                if (validation is not null)
                {
                    return Results.BadRequest(new
                    {
                        message = validation
                    });
                }

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

                var activity = await db.Activities
                    .FirstOrDefaultAsync(
                        x => x.Id == activityId
                            && x.ClubId == clubId,
                        cancellationToken);

                if (activity is null)
                {
                    return Results.NotFound(new
                    {
                        message = "Activity not found in this club."
                    });
                }

                if (activity.Status == ActivityStatuses.Cancelled)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "Attendance cannot be recorded for a cancelled activity."
                    });
                }

                IReadOnlyCollection<ClubMemberRosterItem> members;

                try
                {
                    members = await rosterClient.ResolveAsync(
                        clubId,
                        [memberId],
                        activity.StartTimeUtc,
                        httpContext.GetBearerToken(),
                        cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    return Results.Problem(
                        exception.Message,
                        statusCode:
                            StatusCodes.Status503ServiceUnavailable);
                }

                var member = members.SingleOrDefault();

                if (member is null)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "The member is not eligible for this activity."
                    });
                }

                var status = AttendanceStatuses.Normalize(
                    request.Status);

                var row = await db.ActivityAttendances
                    .Where(x =>
                        x.ActivityId == activityId
                        && x.UserId == member.UserId)
                    .OrderBy(x => x.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                row ??= new ActivityAttendance
                {
                    ActivityId = activityId,
                    UserId = member.UserId,
                    FullName = member.FullName,
                    AttendanceDate = DateOnly.FromDateTime(
                        activity.StartTimeUtc.UtcDateTime)
                };

                if (row.Id == 0)
                {
                    db.ActivityAttendances.Add(row);
                }

                ApplyAttendance(
                    row,
                    status,
                    request.Note,
                    user.GetUserId(),
                    activity.StartTimeUtc);

                await db.SaveChangesAsync(cancellationToken);

                return Results.Ok(
                    new AttendanceMemberResponse(
                        member.Id,
                        member.UserId,
                        member.FullName,
                        member.Email,
                        member.PhoneNumber,
                        member.Role,
                        member.JoinedAtUtc,
                        row.Status,
                        row.Note,
                        row.CheckedInAtUtc,
                        row.CheckedInByUserId));
            })
            .RequireAuthorization(AuthPolicies.BusinessAccess)
            .WithTags("Activity Attendance");
    }

    private static void MapBulkUpdateAttendance(
        IEndpointRouteBuilder app)
    {
        app.MapPut(
            "/api/clubs/{clubId:int}/activities/{activityId:int}/attendance",
            async (
                int clubId,
                int activityId,
                BulkUpdateAttendanceRequest request,
                ActivityDbContext db,
                ClubMemberRosterClient rosterClient,
                ClaimsPrincipal user,
                HttpContext httpContext,
                ClubAccessClient clubAccess,
                ILogger<Program> logger,
                CancellationToken cancellationToken) =>
            {
                var invalid =
                    AttendanceManagementRules.ValidateBulk(
                        request.Items);

                if (invalid is not null)
                {
                    return Results.BadRequest(new
                    {
                        message = invalid
                    });
                }

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

                var activity = await db.Activities
                    .FirstOrDefaultAsync(
                        x => x.Id == activityId
                            && x.ClubId == clubId,
                        cancellationToken);

                if (activity is null)
                {
                    return Results.NotFound(new
                    {
                        message = "Activity not found in this club."
                    });
                }

                if (activity.Status == ActivityStatuses.Cancelled)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "Attendance cannot be recorded for a cancelled activity."
                    });
                }

                IReadOnlyCollection<ClubMemberRosterItem> members;

                try
                {
                    members = await rosterClient.ResolveAsync(
                        clubId,
                        request.Items
                            .Select(x => x.MemberId)
                            .ToArray(),
                        activity.StartTimeUtc,
                        httpContext.GetBearerToken(),
                        cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    return Results.Problem(
                        exception.Message,
                        statusCode:
                            StatusCodes.Status503ServiceUnavailable);
                }

                if (members.Count != request.Items.Count)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "One or more members do not belong to this club or joined after the activity started."
                    });
                }

                IDbContextTransaction? transaction = null;

                try
                {
                    if (db.Database.IsRelational())
                    {
                        transaction =
                            await db.Database.BeginTransactionAsync(
                                cancellationToken);
                    }

                    var userIds = members
                        .Select(x => x.UserId)
                        .ToArray();

                    var existing =
                        await db.ActivityAttendances
                            .Where(x =>
                                x.ActivityId == activityId
                                && userIds.Contains(x.UserId))
                            .OrderBy(x => x.Id)
                            .ToListAsync(cancellationToken);

                    var rowsByUser = existing
                        .GroupBy(x => x.UserId)
                        .ToDictionary(
                            x => x.Key,
                            x => x.First());

                    var requestByMember = request.Items
                        .ToDictionary(x => x.MemberId);

                    foreach (var member in members)
                    {
                        if (!rowsByUser.TryGetValue(
                                member.UserId,
                                out var row))
                        {
                            row = new ActivityAttendance
                            {
                                ActivityId = activityId,
                                UserId = member.UserId,
                                FullName = member.FullName,
                                AttendanceDate =
                                    DateOnly.FromDateTime(
                                        activity
                                            .StartTimeUtc
                                            .UtcDateTime)
                            };

                            db.ActivityAttendances.Add(row);
                        }

                        var item =
                            requestByMember[member.Id];

                        ApplyAttendance(
                            row,
                            AttendanceStatuses.Normalize(
                                item.Status),
                            item.Note,
                            user.GetUserId(),
                            activity.StartTimeUtc);
                    }

                    await db.SaveChangesAsync(
                        cancellationToken);

                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(
                            cancellationToken);
                    }

                    logger.LogInformation(
                        "Attendance updated for {Count} members in activity {ActivityId}, club {ClubId}, by user {UserId}",
                        members.Count,
                        activityId,
                        clubId,
                        user.GetUserId());

                    return Results.Ok(new
                    {
                        updated = members.Count
                    });
                }
                catch
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);
                    }

                    throw;
                }
                finally
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
            })
            .RequireAuthorization(AuthPolicies.BusinessAccess)
            .WithTags("Activity Attendance");
    }
}