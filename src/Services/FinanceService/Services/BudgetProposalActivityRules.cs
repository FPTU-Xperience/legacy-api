namespace FinanceService.Services;

public static class BudgetProposalActivityRules
{
    private static readonly string[] AttendanceKeywords =
    [
        "attendance",
        "roll call",
        "check-in",
        "check in",
        "điểm danh",
        "diem danh"
    ];

    public static bool CanLink(
        IReadOnlyCollection<int>? meetingDays,
        string? title,
        string? description)
    {
        if (meetingDays is { Count: > 0 }) return false;

        var content = $"{title} {description}";
        return !AttendanceKeywords.Any(keyword =>
            content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
