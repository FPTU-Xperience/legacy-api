namespace ExportService.Models;

public sealed class ExportFile
{
    public int Id { get; set; }
    public int ExportRequestId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
    public ExportRequest ExportRequest { get; set; } = null!;
}
