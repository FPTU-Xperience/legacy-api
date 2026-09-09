namespace ActivityService.Services;

public static class WeeklyAttendanceRules
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateOnly GetVietnamDate(DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(utcNow.ToOffset(VietnamOffset).DateTime);

    public static int GetIsoDayOfWeek(DateOnly date) =>
        date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    public static bool IsScheduledDay(IEnumerable<int> meetingDays, DateOnly date) =>
        meetingDays.Contains(GetIsoDayOfWeek(date));

    public static string? ValidateCheckIn(
        IReadOnlyCollection<int> meetingDays,
        DateOnly vietnamDate,
        IEnumerable<DateOnly> existingDates)
    {
        if (meetingDays.Count == 0)
            return "This activity does not have a weekly attendance schedule.";
        if (!IsScheduledDay(meetingDays, vietnamDate))
            return "Attendance is not open for this activity today (Vietnam time).";
        if (existingDates.Contains(vietnamDate))
            return "You have already checked in for this activity today.";
        return null;
    }

    public static int CountScheduledDays(
        IReadOnlyCollection<int> meetingDays,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (meetingDays.Count == 0 || endDate < startDate) return 0;
        var count = 0;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (IsScheduledDay(meetingDays, date)) count++;
        }
        return count;
    }

    public static decimal CalculateRate(int attendedDays, int scheduledDays) =>
        scheduledDays <= 0 ? 0m : Math.Round(attendedDays * 100m / scheduledDays, 2, MidpointRounding.AwayFromZero);
}
