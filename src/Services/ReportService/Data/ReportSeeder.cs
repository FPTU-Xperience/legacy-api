using Microsoft.Extensions.Logging;

namespace ReportService.Data;

public static class ReportSeeder
{
    // NOTE: Report seeding is intentionally left empty.
    // Reports are created through the application workflow, not through database seeding.
    public static Task SeedAsync(ReportDbContext db, ILogger? logger = null)
    {
        logger?.LogInformation("ReportSeeder: No static seed data needed - reports are created via application workflow");
        return Task.CompletedTask;
    }
}
