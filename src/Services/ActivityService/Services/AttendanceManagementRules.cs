using ActivityService.Contracts;
using ActivityService.Models;

namespace ActivityService.Services;

public static class AttendanceManagementRules
{
    public static string? ValidateEntry(string? status, string? note)
    {
        if (string.IsNullOrEmpty(AttendanceStatuses.Normalize(status)))
            return "Attendance status must be NotMarked, Present, Absent, Excused, or Late.";
        if ((note?.Trim().Length ?? 0) > 1000)
            return "Attendance notes cannot exceed 1,000 characters.";
        return null;
    }

    public static string? ValidateBulk(IReadOnlyCollection<BulkAttendanceItemRequest>? items)
    {
        if (items is null || items.Count is < 1 or > 500)
            return "Bulk attendance must contain between 1 and 500 members.";
        if (items.Select(x => x.MemberId).Distinct().Count() != items.Count)
            return "Duplicate members are not allowed in a bulk attendance request.";
        return items.Select(x => ValidateEntry(x.Status, x.Note)).FirstOrDefault(x => x is not null);
    }

    public static string? ValidateActivity(string activityStatus) =>
        string.Equals(activityStatus, ActivityStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            ? "Attendance cannot be recorded for a cancelled activity."
            : null;

    public static string? ValidateEligibility(int requestedClubId, int activityClubId, DateTimeOffset joinedAtUtc, DateTimeOffset activityStartUtc)
    {
        if (requestedClubId != activityClubId) return "The activity belongs to another club.";
        if (joinedAtUtc > activityStartUtc) return "The member joined after the activity started.";
        return null;
    }

    public static bool CanManage(bool isAdministrator, bool isClubManager) => isAdministrator || isClubManager;
}
