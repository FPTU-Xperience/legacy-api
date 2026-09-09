using System;

namespace ReportService.Models;

public sealed class ReportUploadedFile
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;

    // Preview Metadata
    public string? PreviewFileName { get; set; }
    public string? PreviewStoragePath { get; set; }
    public string? PreviewContentType { get; set; }
    public string? PreviewStatus { get; set; } // "Available", "Pending", "Failed", "Unsupported", "None"
    public string? PreviewErrorMessage { get; set; }
    public DateTimeOffset? PreviewGeneratedAtUtc { get; set; }

    public Report Report { get; set; } = null!;
}
