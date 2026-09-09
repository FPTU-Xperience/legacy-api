namespace ReportService.Services;

public sealed record FutureEventDetailInput(
    DateOnly ActivityDate,
    string? ActivityName,
    string? Description,
    string? Location);

public static class FutureEventReportRules
{
    public const string ReportType = "FUTURE_EVENT";

    public static bool IsFutureEvent(string? reportType) =>
        string.Equals(reportType?.Trim(), ReportType, StringComparison.OrdinalIgnoreCase);

    public static string? Validate(
        string? reportType,
        IReadOnlyCollection<FutureEventDetailInput> details,
        DateOnly vietnamToday)
    {
        if (!IsFutureEvent(reportType)) return null;
        if (details.Count != 1) return "A future event proposal must contain exactly one planned event.";

        var detail = details.Single();
        if (detail.ActivityDate <= vietnamToday) return "The planned event date must be in the future (Vietnam time).";
        if (string.IsNullOrWhiteSpace(detail.ActivityName)
            || string.IsNullOrWhiteSpace(detail.Description)
            || string.IsNullOrWhiteSpace(detail.Location))
        {
            return "Event name, description, and location are required for a future event proposal.";
        }

        return null;
    }

    public static bool CanExposeFinance(bool isFutureEvent, bool isPrivilegedViewer) =>
        !isFutureEvent || isPrivilegedViewer;
}
