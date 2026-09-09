using Microsoft.Extensions.Logging;

namespace FinanceService.Data;

public static class FinanceSeeder
{
    // NOTE: Finance seeding is intentionally left empty.
    // Budget proposals and settlements are created through the application workflow, not through database seeding.
    public static Task SeedAsync(FinanceDbContext db, ILogger? logger = null)
    {
        logger?.LogInformation("FinanceSeeder: No static seed data needed - finances are managed via application workflow");
        return Task.CompletedTask;
    }
}
