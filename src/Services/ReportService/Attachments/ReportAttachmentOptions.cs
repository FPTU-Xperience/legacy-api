namespace ReportService.Attachments;

public sealed class ReportAttachmentOptions
{
    public const string SectionName = "Attachments";

    public string StoragePath { get; set; } = "attachments";
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/png",
        "image/jpeg",
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    ];
    public string[] AllowedExtensions { get; set; } =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".pdf",
        ".xlsx"
    ];
}
