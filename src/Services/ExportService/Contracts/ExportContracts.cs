namespace ExportService.Contracts;

public sealed record CreateExportRequest(
    string ExportType,
    string Scope,
    string? Period,
    int? ClubId,
    int? ReportId);

public sealed record ExportResponse(
    int Id,
    string ExportType,
    string Scope,
    string Status,
    string? Period,
    int? ClubId,
    int? ReportId,
    int RequestedByUserId,
    string RequestedByName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? ErrorMessage,
    ExportFileResponse? File,
    bool IsDownloadAvailable = false);

public sealed record ExportFileResponse(
    int Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset ExpiresAtUtc,
    string Checksum,
    bool IsAvailable);
