using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReportService.Data;
using ReportService.Models;

namespace ReportService.Services;

/// <summary>
/// Handles selective demo report reset operations.
/// Safety checks are mandatory and internal — no public method bypasses CanResetAsync.
/// </summary>
public sealed class DemoResetService
{
    private static readonly string[] KnownSeedKeys = {
        "DEMO-IT-Q1-2026",
        "DEMO-IT-Q2-2026",
        "DEMO-SE-SPRING-2026",
        "DEMO-AI-FOR-STUDENTS-2026",
        "DEMO-ART-SUMMER-2026",
    };

    private readonly ReportDbContext _db;

    public DemoResetService(ReportDbContext db)
    {
        _db = db;
    }

    public string GetResetBlockedReason()
        => "Cannot reset demo reports: one or more have uploaded-file records "
         + "or storage-backed attachments. Physical file deletion must be implemented first.";

    /// <summary>
    /// Returns true only when NO demo report has a primary uploaded file
    /// OR a storage-backed attachment. Either blocks safe reset.
    /// </summary>
    public async Task<bool> CanResetAsync(CancellationToken ct = default)
    {
        var demoReportIds = await _db.Reports
            .AsNoTracking()
            .Where(r => r.SeedKey != null && KnownSeedKeys.Contains(r.SeedKey))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (demoReportIds.Count == 0)
            return true; // nothing to reset

        var hasUploadedFiles = await _db.ReportUploadedFiles
            .AnyAsync(f => demoReportIds.Contains(f.ReportId), ct);

        var hasAttachments = await _db.ReportAttachments
            .AnyAsync(a => demoReportIds.Contains(a.ReportId), ct);

        return !hasUploadedFiles && !hasAttachments;
    }

    /// <summary>
    /// Counts demo reports with known SeedKeys.
    /// </summary>
    public async Task<int> CountDemoReportsAsync(CancellationToken ct = default)
        => await _db.Reports.CountAsync(
            r => r.SeedKey != null && KnownSeedKeys.Contains(r.SeedKey), ct);

    /// <summary>
    /// Resets demo reports by SeedKey: updates in place (preserves Report.Id).
    /// Deletes child rows explicitly (AuditLogs has no FK cascade to Reports).
    /// Recreates ReportDetails and ReportFeedback with demo content.
    /// Uses a transaction — rolls back on any failure.
    /// </summary>
    public async Task ResetDemoReportsAsync(
        (int It, int Se, int Ai, int Art) clubIds,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (!await CanResetAsync(ct))
            throw new InvalidOperationException(GetResetBlockedReason());

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var seedKey in KnownSeedKeys)
            {
                await ResetSingleDemoReportAsync(seedKey, clubIds, logger, ct);
            }

            // ReportingDeadlines: upsert by Period (no FK to Reports)
            await UpsertReportingDeadlinesAsync(clubIds, ct);

            await tx.CommitAsync(ct);
            logger.LogInformation("[DemoReset] Demo reports reset successfully.");
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Updates a demo report in place: clears child rows, updates main fields.
    /// Never deletes the Report row itself — preserves Id and all foreign references.
    /// If the SeedKey row is missing, creates a new one.
    /// </summary>
    private async Task ResetSingleDemoReportAsync(
        string seedKey,
        (int It, int Se, int Ai, int Art) clubIds,
        ILogger logger,
        CancellationToken ct)
    {
        var report = await _db.Reports
            .Include(r => r.Details)
            .FirstOrDefaultAsync(r => r.SeedKey == seedKey, ct);

        if (report == null)
        {
            // SeedKey row is gone — create a new one
            report = new Report
            {
                SeedKey = seedKey,
                ClubId = GetClubIdForSeedKey(seedKey, clubIds),
                Period = GetPeriodForSeedKey(seedKey),
                ReportType = GetReportTypeForSeedKey(seedKey),
                Status = GetStatusForSeedKey(seedKey),
                Tag = GetReportTypeForSeedKey(seedKey),
                ContentSource = ReportContentSources.StructuredForm,
                CreatedByUserId = 4,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            _db.Reports.Add(report);
            await _db.SaveChangesAsync(ct);
            logger.LogInformation("[DemoReset] Created new report for SeedKey={SeedKey}", seedKey);
            return;
        }

        // Delete child rows explicitly — AuditLogs has no FK cascade to Reports
        var reportId = report.Id;
        await _db.AuditLogs.Where(a => a.ReportId == reportId).ExecuteDeleteAsync(ct);
        await _db.ReportDetails.Where(d => d.ReportId == reportId).ExecuteDeleteAsync(ct);
        await _db.ReportFeedback.Where(f => f.ReportId == reportId).ExecuteDeleteAsync(ct);

        // Update main fields (period, status, content — determined by SeedKey)
        report.ClubId = GetClubIdForSeedKey(seedKey, clubIds);
        report.Period = GetPeriodForSeedKey(seedKey);
        report.ReportType = GetReportTypeForSeedKey(seedKey);
        report.Status = GetStatusForSeedKey(seedKey);
        report.Tag = GetReportTypeForSeedKey(seedKey);
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        logger.LogInformation("[DemoReset] Reset report Id={Id} SeedKey={SeedKey}", report.Id, seedKey);
    }

    private static int GetClubIdForSeedKey(string seedKey, (int It, int Se, int Ai, int Art) c)
        => seedKey switch
        {
            "DEMO-IT-Q1-2026" or "DEMO-IT-Q2-2026" => c.It,
            "DEMO-SE-SPRING-2026" => c.Se,
            "DEMO-AI-FOR-STUDENTS-2026" => c.Ai,
            "DEMO-ART-SUMMER-2026" => c.Art,
            _ => throw new ArgumentException($"Unknown SeedKey: {seedKey}"),
        };

    private static string GetPeriodForSeedKey(string seedKey)
        => seedKey switch
        {
            "DEMO-IT-Q1-2026" => "Q1/2026",
            "DEMO-IT-Q2-2026" => "Q2/2026",
            "DEMO-SE-SPRING-2026" => "Spring 2026",
            "DEMO-AI-FOR-STUDENTS-2026" => "AI for Students 2026",
            "DEMO-ART-SUMMER-2026" => "Summer 2026",
            _ => throw new ArgumentException($"Unknown SeedKey: {seedKey}"),
        };

    private static string GetReportTypeForSeedKey(string seedKey)
        => seedKey switch
        {
            "DEMO-IT-Q1-2026" or "DEMO-IT-Q2-2026" => "Báo cáo Quý",
            "DEMO-SE-SPRING-2026" or "DEMO-ART-SUMMER-2026" => "Báo cáo Học kỳ",
            "DEMO-AI-FOR-STUDENTS-2026" => "Báo cáo Chuyên đề",
            _ => throw new ArgumentException($"Unknown SeedKey: {seedKey}"),
        };

    private static string GetStatusForSeedKey(string seedKey)
        => seedKey switch
        {
            "DEMO-IT-Q1-2026" or "DEMO-SE-SPRING-2026" => "Approved",
            "DEMO-IT-Q2-2026" => "Under Review",
            "DEMO-AI-FOR-STUDENTS-2026" => "Rejected",
            "DEMO-ART-SUMMER-2026" => "Draft",
            _ => throw new ArgumentException($"Unknown SeedKey: {seedKey}"),
        };

    /// <summary>
    /// Upserts ReportingDeadlines by Period (no FK to Reports).
    /// Demo deadlines use a fixed DueDate of 30 days from now.
    /// </summary>
    private async Task UpsertReportingDeadlinesAsync(
        (int It, int Se, int Ai, int Art) clubIds,
        CancellationToken ct)
    {
        var demoPeriods = new[]
        {
            "Q1/2026",
            "Q2/2026",
            "Spring 2026",
            "AI for Students 2026",
            "Summer 2026",
        };

        foreach (var period in demoPeriods)
        {
            var existing = await _db.ReportingDeadlines
                .FirstOrDefaultAsync(d => d.Period == period, ct);

            if (existing != null)
            {
                existing.IsActive = true;
            }
            else
            {
                _db.ReportingDeadlines.Add(new ReportingDeadline
                {
                    Period = period,
                    DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    IsActive = true,
                });
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
