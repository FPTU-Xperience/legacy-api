using Microsoft.Extensions.Logging;

namespace ActivityService.Data;

public static class ActivitySeeder
{
    // NOTE: Activity seeding is intentionally left empty.
    // Activities are created through the application workflow, not through database seeding.
    public static Task SeedAsync(ActivityDbContext db, ILogger? logger = null)
    {
        logger?.LogInformation("ActivitySeeder: No static seed data needed - activities are created via application workflow");
        return Task.CompletedTask;
    }
}
