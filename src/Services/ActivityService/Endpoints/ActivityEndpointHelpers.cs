using System.Security.Claims;
using ActivityService.Contracts;
using ActivityService.Infrastructure;
using ActivityService.Models;
using ActivityService.Services;
using ClubReportHub.Shared.Auth;

namespace ActivityService.Endpoints;

internal static class ActivityEndpointHelpers
{
    internal static bool CanReviewAllActivities(
        ClaimsPrincipal user)
    {
        return user.IsInRole(AuthRoles.Admin)
            || user.IsInRole(AuthRoles.StudentAffairsAdmin);
    }

    internal static ActivityResponse ToResponse(
        ClubActivity activity)
    {
        return new ActivityResponse(
            activity.Id,
            activity.ClubId,
            activity.ClubName,
            activity.Title,
            activity.Description,
            activity.StartTimeUtc,
            activity.EndTimeUtc,
            GetMeetingDays(activity.MeetingDaysCsv),
            activity.Location,
            activity.Status,
            activity.CreatedByUserId,
            activity.CreatedAtUtc,
            activity.Participants
                .OrderBy(x => x.RegisteredAtUtc)
                .Select(x => new ActivityParticipantResponse(
                    x.Id,
                    x.UserId,
                    x.FullName,
                    x.AttendanceStatus,
                    x.RegisteredAtUtc))
                .ToArray(),
            activity.Attendances
                .OrderByDescending(x => x.AttendanceDate)
                .ThenBy(x => x.FullName)
                .Select(x => new ActivityAttendanceResponse(
                    x.Id,
                    x.UserId,
                    x.FullName,
                    x.AttendanceDate,
                    x.Status,
                    x.Note,
                    x.CheckedInAtUtc,
                    x.CheckedInByUserId))
                .ToArray());
    }

    internal static IReadOnlyList<int> NormalizeMeetingDays(
        IEnumerable<int>? meetingDays)
    {
        return meetingDays?
            .Where(day => day is >= 1 and <= 7)
            .Distinct()
            .OrderBy(day => day)
            .ToArray()
            ?? [];
    }

    internal static IReadOnlyList<int> GetMeetingDays(
        string? meetingDaysCsv)
    {
        if (string.IsNullOrWhiteSpace(meetingDaysCsv))
        {
            return [];
        }

        return NormalizeMeetingDays(
            meetingDaysCsv
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Select(value =>
                    int.TryParse(value, out var day)
                        ? day
                        : 0));
    }

    internal static async Task<bool> CanManageClubAsync(
        int clubId,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var access = await clubAccess.GetMyAccessAsync(
            httpContext.GetBearerToken(),
            cancellationToken);

        return access.Any(
            x => x.ClubId == clubId && x.CanManage);
    }

    internal static async Task<bool> CanManageClubOrReviewAllAsync(
        int clubId,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        return CanReviewAllActivities(user)
            || await CanManageClubAsync(
                clubId,
                clubAccess,
                httpContext,
                cancellationToken);
    }

    internal static string? ValidateAttendance(
        string? status,
        string? note)
    {
        return AttendanceManagementRules.ValidateEntry(
            status,
            note);
    }

    internal static void ApplyAttendance(
        ActivityAttendance row,
        string status,
        string? note,
        int checkedByUserId,
        DateTimeOffset activityStartTimeUtc)
    {
        row.Status = status;

        row.Note = string.IsNullOrWhiteSpace(note)
            ? null
            : note.Trim();

        row.UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (status == AttendanceStatuses.NotMarked)
        {
            row.CheckedInAtUtc = null;
            row.CheckedInByUserId = null;
            return;
        }

        row.CheckedInByUserId = checkedByUserId;

        row.CheckedInAtUtc =
            DateTimeOffset.UtcNow >= activityStartTimeUtc
                ? DateTimeOffset.UtcNow
                : null;
    }
}