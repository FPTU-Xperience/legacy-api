using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Messaging;
using ExportService.Data;
using ExportService.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace ExportService.Services;

public sealed class ExportGenerationJob(
    ExportDbContext db,
    ExportFileGenerator generator,
    IEventBus eventBus,
    IConfiguration configuration,
    ILogger<ExportGenerationJob> logger)
{
    [Queue("exports")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [2, 5])]
    public async Task GenerateAsync(int requestId, CancellationToken cancellationToken)
    {
        var request = await db.ExportRequests
            .Include(x => x.File)
            .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (request is null || request.Status == ExportStatuses.Completed)
        {
            return;
        }

        request.Status = ExportStatuses.Processing;
        request.ErrorMessage = null;
        request.CompletedAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var generated = generator.Generate(request);
            var retentionHours = Math.Clamp(
                configuration.GetValue("Exports:RetentionHours", 24),
                1,
                24 * 30);

            request.File ??= new ExportFile { ExportRequestId = request.Id };
            request.File.FileName = generated.FileName;
            request.File.ContentType = generated.ContentType;
            request.File.FilePath = generated.FilePath;
            request.File.SizeBytes = generated.SizeBytes;
            request.File.Checksum = generated.Checksum;
            request.File.ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(retentionHours);
            request.File.IsAvailable = true;
            request.Status = ExportStatuses.Completed;
            request.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to generate export request {ExportRequestId}.", requestId);
            request.Status = ExportStatuses.Failed;
            request.ErrorMessage = "Không thể tạo tệp xuất. Hệ thống sẽ tự động thử lại.";
            request.CompletedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        try
        {
            await eventBus.PublishAsync(
                new ExportCompletedEvent(
                    Guid.NewGuid(),
                    DateTimeOffset.UtcNow,
                    request.Id,
                    request.ExportType,
                    request.File!.FileName,
                    request.RequestedByUserId),
                EventRoutingKeys.ExportCompleted,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Export {ExportRequestId} completed but its notification event could not be published.",
                requestId);
        }
    }
}
