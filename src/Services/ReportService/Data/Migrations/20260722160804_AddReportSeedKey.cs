using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSeedKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add SeedKey column only if it doesn't already exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = 'Reports' AND COLUMN_NAME = 'SeedKey'
                )
                BEGIN
                    ALTER TABLE [Reports] ADD [SeedKey] varchar(50) NULL;
                END;
            ");

            // Create filtered unique index only if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Reports_SeedKey' AND object_id = OBJECT_ID('Reports')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Reports_SeedKey]
                    ON [Reports] ([SeedKey])
                    WHERE [SeedKey] IS NOT NULL;
                END;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_Reports_SeedKey] ON [Reports];");
            migrationBuilder.Sql("ALTER TABLE [Reports] DROP COLUMN IF EXISTS [SeedKey];");
        }
    }
}
