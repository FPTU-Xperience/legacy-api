using ExportService.Contracts;
using ExportService.Models;

namespace ExportService.Extensions;

public static class ExportExtensions
{
    public static Dictionary<string, string[]> Validate(CreateExportRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        if (ExportTypes.Normalize(input.ExportType) is null)
        {
            errors[nameof(input.ExportType)] = ["ExportType must be PDF, XLSX, or DOCX."];
        }

        if (string.IsNullOrWhiteSpace(input.Scope) || input.Scope.Trim().Length > 40)
        {
            errors[nameof(input.Scope)] = ["Scope is required and must not exceed 40 characters."];
        }

        if (input.Period?.Trim().Length > 40)
        {
            errors[nameof(input.Period)] = ["Period must not exceed 40 characters."];
        }

        if (input.ClubId is <= 0)
        {
            errors[nameof(input.ClubId)] = ["ClubId must be a positive number."];
        }

        if (input.Scope.Trim().Equals("Report", StringComparison.OrdinalIgnoreCase) && input.ReportId is null or <= 0)
        {
            errors[nameof(input.ReportId)] = ["ReportId is required when Scope is 'Report'."];
        }

        return errors;
    }

    public static ExportResponse ToResponse(ExportRequest request)
    {
        bool isAvailable = request.File is not null
            && request.File.IsAvailable
            && request.File.ExpiresAtUtc > DateTimeOffset.UtcNow
            && File.Exists(request.File.FilePath);

        return new ExportResponse(
            request.Id,
            request.ExportType,
            request.Scope,
            request.Status,
            request.Period,
            request.ClubId,
            request.ReportId,
            request.RequestedByUserId,
            request.RequestedByName,
            request.CreatedAtUtc,
            request.CompletedAtUtc,
            request.ErrorMessage,
            request.File is null
                ? null
                : new ExportFileResponse(
                    request.File.Id,
                    request.File.FileName,
                    request.File.ContentType,
                    request.File.SizeBytes,
                    request.File.ExpiresAtUtc,
                    request.File.Checksum,
                    isAvailable),
            isAvailable);
    }
}
