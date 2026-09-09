using ActivityService.Data;
using ClubReportHub.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace ActivityService.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeActivityDatabaseAsync(
        this WebApplication app)
    {
        await using var scope =
            app.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ActivityDbContext>();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseStartup");

        await db.EnsureCreatedWithRetryAsync(logger);

        await ActivitySchemaUpgrader.ApplyAsync(db);

        await ActivitySeeder.SeedAsync(db);
    }
}