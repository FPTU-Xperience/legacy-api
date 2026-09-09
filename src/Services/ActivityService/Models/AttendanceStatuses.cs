namespace ActivityService.Models;

public static class AttendanceStatuses
{
    public const string Registered = "Registered";
    public const string Attended = "Attended";
    public const string Absent = "Absent";

    public const string NotMarked = "NotMarked";
    public const string Present = "Present";
    public const string Excused = "Excused";
    public const string Late = "Late";

    public static readonly IReadOnlySet<string> Manageable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NotMarked, Present, Absent, Excused, Late
    };

    public static string Normalize(string? value) => Manageable
        .FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? string.Empty;
}
