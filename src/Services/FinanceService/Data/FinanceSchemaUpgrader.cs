using Microsoft.EntityFrameworkCore;

namespace FinanceService.Data;

public static class FinanceSchemaUpgrader
{
    public static Task ApplyAsync(FinanceDbContext db, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF COL_LENGTH(N'dbo.BudgetProposals', N'ManagerReviewedByUserId') IS NULL
                ALTER TABLE [dbo].[BudgetProposals] ADD [ManagerReviewedByUserId] int NULL;

            IF COL_LENGTH(N'dbo.BudgetProposals', N'ManagerReviewedAtUtc') IS NULL
                ALTER TABLE [dbo].[BudgetProposals] ADD [ManagerReviewedAtUtc] datetimeoffset NULL;

            IF COL_LENGTH(N'dbo.BudgetProposals', N'ManagerReviewNote') IS NULL
                ALTER TABLE [dbo].[BudgetProposals] ADD [ManagerReviewNote] nvarchar(1000) NULL;

            IF COL_LENGTH(N'dbo.BudgetProposals', N'SourceReportId') IS NULL
                ALTER TABLE [dbo].[BudgetProposals] ADD [SourceReportId] int NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[BudgetProposals]')
                  AND name = N'IX_BudgetProposals_SourceReportId')
                EXEC(N'CREATE UNIQUE INDEX [IX_BudgetProposals_SourceReportId]
                    ON [dbo].[BudgetProposals] ([SourceReportId])
                    WHERE [SourceReportId] IS NOT NULL');

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[BudgetProposals]')
                  AND name = N'IX_BudgetProposals_ProposedAtUtc')
                EXEC(N'CREATE INDEX [IX_BudgetProposals_ProposedAtUtc]
                    ON [dbo].[BudgetProposals] ([ProposedAtUtc] DESC)');

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[BudgetProposals]')
                  AND name = N'IX_BudgetProposals_ProposedByUserId')
                EXEC(N'CREATE INDEX [IX_BudgetProposals_ProposedByUserId]
                    ON [dbo].[BudgetProposals] ([ProposedByUserId])');

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Settlements]')
                  AND name = N'IX_Settlements_BudgetProposalId')
                EXEC(N'CREATE INDEX [IX_Settlements_BudgetProposalId]
                    ON [dbo].[Settlements] ([BudgetProposalId])');

            IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[OutboxMessages] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [OccurredAtUtc] datetimeoffset NOT NULL,
                    [EventType] nvarchar(100) NOT NULL,
                    [EventTypeName] nvarchar(300) NOT NULL,
                    [Payload] nvarchar(max) NOT NULL,
                    [Status] nvarchar(50) NOT NULL,
                    [RetryCount] int NOT NULL DEFAULT 0,
                    [ProcessedAtUtc] datetimeoffset NULL,
                    [ErrorMessage] nvarchar(max) NULL,
                    [CorrelationId] nvarchar(100) NULL
                );
                CREATE INDEX [IX_OutboxMessages_Status_OccurredAtUtc]
                    ON [dbo].[OutboxMessages] ([Status], [OccurredAtUtc]);
            END
            """;

        return db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
