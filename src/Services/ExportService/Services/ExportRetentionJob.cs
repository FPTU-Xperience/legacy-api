using ExportService.Data;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ExportService.Services;

public sealed class ExportRetentionJob(
    ExportDbContext db,
    ILogger<ExportRetentionJob> logger)
{
    [Queue("exports")]
    [AutomaticRetry(Attempts = 1)]
    public async Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredFiles = await db.ExportFiles
            .Where(x => x.IsAvailable && x.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var file in expiredFiles)
        {
            file.IsAvailable = false;
            if (!File.Exists(file.FilePath))
            {
                continue;
            }

            try
            {
                File.Delete(file.FilePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(
                    exception,
                    "Could not remove expired export file {ExportFileId} at {FilePath}.",
                    file.Id,
                    file.FilePath);
            }
        }

        if (expiredFiles.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Marked {Count} expired export files as unavailable.", expiredFiles.Count);
        }
    }
}
