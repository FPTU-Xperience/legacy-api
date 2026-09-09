using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace ExportService.Data;

public sealed class ExportDbContextFactory : IDesignTimeDbContextFactory<ExportDbContext>
{
    public ExportDbContext CreateDbContext(string[] args)
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
            connectionString = "Server=localhost;Database=ClubReportHub_Export;Trusted_Connection=True;TrustServerCertificate=True";
        }

        var options = new DbContextOptionsBuilder<ExportDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ExportDbContext(options);
    }
}
