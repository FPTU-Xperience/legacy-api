namespace ExportService.Models;

public sealed class ExportRequest
{
    public int Id { get; set; }
    public string ExportType { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Status { get; set; } = ExportStatuses.Pending;
    public string? Period { get; set; }
    public int? ClubId { get; set; }
    public int? ReportId { get; set; }
    public int RequestedByUserId { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public string CriteriaJson { get; set; } = "{}";
    public string? SnapshotJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public ExportFile? File { get; set; }
}

public static class ExportStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class ExportTypes
{
    public const string Pdf = "PDF";
    public const string Excel = "XLSX";
    public const string Docx = "DOCX";

    public static string? Normalize(string? value)
    {
        return value?.Trim().ToUpperInvariant() switch
        {
            "PDF" => Pdf,
            "XLSX" or "EXCEL" => Excel,
            "DOCX" or "WORD" => Docx,
            _ => null
        };
    }
}
