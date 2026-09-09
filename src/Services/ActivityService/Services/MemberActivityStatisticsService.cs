using ActivityService.Contracts;
using ActivityService.Data;
using ActivityService.Models;
using Microsoft.EntityFrameworkCore;

namespace ActivityService.Services;

public sealed record EligibleActivity(int Id, int ClubId, string Title, DateTimeOffset StartTimeUtc, string Status);
public sealed record RecordedAttendance(int ActivityId, int UserId, string Status);

public static class MemberActivityStatisticsCalculator
{
    public static MemberStatisticsResponse Calculate(
        MemberStatisticsInput member,
        IEnumerable<EligibleActivity> activities,
        IEnumerable<RecordedAttendance> attendances,
        DateTimeOffset now)
    {
        var eligibleIds = activities
            .Where(x => x.StartTimeUtc >= member.JoinedAtUtc
                && x.StartTimeUtc <= now
                && !string.Equals(x.Status, ActivityStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToHashSet();

        var attended = attendances
            .Where(x => x.UserId == member.UserId
                && eligibleIds.Contains(x.ActivityId)
                && string.Equals(x.Status, AttendanceStatuses.Present, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ActivityId)
            .Distinct()
            .Count();

        var eligible = eligibleIds.Count;
        var rate = eligible == 0 ? 0m : Math.Round(attended * 100m / eligible, 2, MidpointRounding.AwayFromZero);
        return new MemberStatisticsResponse(member.UserId, eligible, attended, rate);
    }
}

public sealed class MemberActivityStatisticsService(ActivityDbContext db)
{
    public async Task<IReadOnlyDictionary<int, MemberStatisticsResponse>> GetBatchAsync(
        int clubId,
        IReadOnlyCollection<MemberStatisticsInput> members,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return new Dictionary<int, MemberStatisticsResponse>();
        }

        var earliestJoin = members.Min(x => x.JoinedAtUtc);
        var activities = await db.Activities.AsNoTracking()
            .Where(x => x.ClubId == clubId
                && x.StartTimeUtc >= earliestJoin
                && x.StartTimeUtc <= now
                && x.Status != ActivityStatuses.Cancelled)
            .Select(x => new EligibleActivity(x.Id, x.ClubId, x.Title, x.StartTimeUtc, x.Status))
            .ToListAsync(cancellationToken);

        var activityIds = activities.Select(x => x.Id).ToArray();
        var userIds = members.Select(x => x.UserId).Distinct().ToArray();
        var attendances = activityIds.Length == 0
            ? []
            : await db.ActivityAttendances.AsNoTracking()
                .Where(x => activityIds.Contains(x.ActivityId) && userIds.Contains(x.UserId))
                .Select(x => new RecordedAttendance(x.ActivityId, x.UserId, x.Status))
                .ToListAsync(cancellationToken);

        return members
            .GroupBy(x => x.UserId)
            .Select(x => x.First())
            .ToDictionary(x => x.UserId, x => MemberActivityStatisticsCalculator.Calculate(x, activities, attendances, now));
    }

    public async Task<MemberStatisticsDetailResponse> GetDetailAsync(
        int clubId,
        MemberStatisticsDetailQuery request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var activities = await db.Activities.AsNoTracking()
            .Where(x => x.ClubId == clubId
                && x.StartTimeUtc >= request.JoinedAtUtc
                && x.StartTimeUtc <= now
                && x.Status != ActivityStatuses.Cancelled)
            .OrderByDescending(x => x.StartTimeUtc)
            .Select(x => new EligibleActivity(x.Id, x.ClubId, x.Title, x.StartTimeUtc, x.Status))
            .ToListAsync(cancellationToken);

        var activityIds = activities.Select(x => x.Id).ToArray();
        var attendances = activityIds.Length == 0
            ? []
            : await db.ActivityAttendances.AsNoTracking()
                .Where(x => activityIds.Contains(x.ActivityId) && x.UserId == request.UserId)
                .Select(x => new RecordedAttendance(x.ActivityId, x.UserId, x.Status))
                .ToListAsync(cancellationToken);

        var statistics = MemberActivityStatisticsCalculator.Calculate(
            new MemberStatisticsInput(request.UserId, request.JoinedAtUtc), activities, attendances, now);
        var attendanceByActivity = attendances
            .GroupBy(x => x.ActivityId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(a => a.Status == AttendanceStatuses.Present).First().Status);
        var totalItems = activities.Count;
        var items = activities.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MemberActivityHistoryResponse(
                x.Id,
                x.Title,
                x.StartTimeUtc,
                x.Status,
                attendanceByActivity.GetValueOrDefault(x.Id, AttendanceStatuses.NotMarked)))
            .ToArray();

        return new MemberStatisticsDetailResponse(
            statistics, items, page, pageSize, totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize));
    }
}
