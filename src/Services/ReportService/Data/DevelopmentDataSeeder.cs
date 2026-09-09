using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReportService.Models;
using ReportService.Options;
using ReportService.Services;

namespace ReportService.Data;

/// <summary>
/// Deterministic development/demo data seeder for ClubReportHub ReportService.
/// Scope: ReportService database only (ClubReportHub_Report).
/// Only active when DemoData:Enabled is true AND environment is Development or Docker.
/// NEVER active in Production.
/// Idempotent: re-running does not create duplicates.
/// Preserves: all non-demo reports, users, clubs.
/// </summary>
public static class DevelopmentDataSeeder
{
    private static readonly string[] KnownSeedKeys = {
        "DEMO-IT-Q1-2026",
        "DEMO-IT-Q2-2026",
        "DEMO-SE-SPRING-2026",
        "DEMO-AI-FOR-STUDENTS-2026",
        "DEMO-ART-SUMMER-2026",
    };

    private const int ReportCreatorUserId = 4; // studentaffairs@club.local

    // ══════════════════════════════════════════════════════════════════════════
    // Public entry point — called from Program.cs startup
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Three-state seeding:
    /// - Enabled=false: no-op
    /// - Enabled=true, ResetReports=false: upsert missing SeedKeys only; no deletion
    /// - Enabled=true, ResetReports=true: in-place reset + deadline upsert
    /// </summary>
    public static async Task SeedAsync(
        ReportDbContext reportDb,
        DemoResetService resetService,
        (int It, int Se, int Ai, int Art) clubIds,
        bool resetReports,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (resetReports)
        {
            logger.LogInformation("[DevSeeder] DemoData:ResetReports is true — running full in-place reset...");
            await resetService.ResetDemoReportsAsync(clubIds, logger, ct);
        }
        else
        {
            logger.LogInformation("[DevSeeder] Upserting missing demo SeedKeys (no deletion)...");
            await UpsertMissingSeedKeysAsync(reportDb, clubIds, logger, ct);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Upsert missing SeedKeys (ResetReports=false path)
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task UpsertMissingSeedKeysAsync(
        ReportDbContext db,
        (int It, int Se, int Ai, int Art) clubIds,
        ILogger logger,
        CancellationToken ct)
    {
        foreach (var seedKey in KnownSeedKeys)
        {
            var exists = await db.Reports.AnyAsync(r => r.SeedKey == seedKey, ct);
            if (exists)
            {
                logger.LogInformation("[DevSeeder] SeedKey={SeedKey} already exists — skipping", seedKey);
                continue;
            }

            var (clubId, period, reportType, status, tag) = GetReportDefinition(seedKey, clubIds);
            db.Reports.Add(new Report
            {
                SeedKey = seedKey,
                ClubId = clubId,
                Period = period,
                ReportType = reportType,
                Status = status,
                Tag = tag,
                ContentSource = ReportContentSources.StructuredForm,
                CreatedByUserId = ReportCreatorUserId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("[DevSeeder] Created new report for SeedKey={SeedKey}", seedKey);
        }

        logger.LogInformation("[DevSeeder] Upsert complete.");
    }

    private static (int ClubId, string Period, string ReportType, string Status, string Tag)
        GetReportDefinition(string seedKey, (int It, int Se, int Ai, int Art) c)
        => seedKey switch
        {
            "DEMO-IT-Q1-2026" => (c.It, "Q1/2026", "Báo cáo Quý", "Approved", "Báo cáo Quý"),
            "DEMO-IT-Q2-2026" => (c.It, "Q2/2026", "Báo cáo Quý", "Under Review", "Báo cáo Quý"),
            "DEMO-SE-SPRING-2026" => (c.Se, "Spring 2026", "Báo cáo Học kỳ", "Approved", "Báo cáo Học kỳ"),
            "DEMO-AI-FOR-STUDENTS-2026" => (c.Ai, "AI for Students 2026", "Báo cáo Chuyên đề", "Rejected", "Báo cáo Chuyên đề"),
            "DEMO-ART-SUMMER-2026" => (c.Art, "Summer 2026", "Báo cáo Học kỳ", "Draft", "Báo cáo Học kỳ"),
            _ => throw new ArgumentException($"Unknown SeedKey: {seedKey}"),
        };
}
