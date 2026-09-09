using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace ClubService.Data;

public sealed class ClubDbContextFactory : IDesignTimeDbContextFactory<ClubDbContext>
{
    public ClubDbContext CreateDbContext(string[] args)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        string? connectionString = null;

        if (File.Exists(configPath))
        {
            var json = JsonDocument.Parse(File.ReadAllText(configPath));
            connectionString = json.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString();
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Server=localhost;Database=ClubReportHub_Club;Trusted_Connection=True;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<ClubDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ClubDbContext(options);
    }
}
