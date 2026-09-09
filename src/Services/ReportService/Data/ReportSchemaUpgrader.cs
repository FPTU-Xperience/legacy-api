using Microsoft.EntityFrameworkCore;

namespace ReportService.Data;

public static class ReportSchemaUpgrader
{
    public static Task ApplyAsync(ReportDbContext db, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF COL_LENGTH(N'dbo.Reports', N'BudgetProposalId') IS NULL
                ALTER TABLE [dbo].[Reports] ADD [BudgetProposalId] int NULL;
            IF COL_LENGTH(N'dbo.Reports', N'BudgetRequestedAmount') IS NULL
                ALTER TABLE [dbo].[Reports] ADD [BudgetRequestedAmount] decimal(18,2) NULL;
            IF COL_LENGTH(N'dbo.Reports', N'BudgetApprovedAmount') IS NULL
                ALTER TABLE [dbo].[Reports] ADD [BudgetApprovedAmount] decimal(18,2) NULL;
            IF COL_LENGTH(N'dbo.Reports', N'BudgetDescription') IS NULL
                ALTER TABLE [dbo].[Reports] ADD [BudgetDescription] nvarchar(2000) NULL;
            IF COL_LENGTH(N'dbo.Reports', N'FinanceSubmittedAtUtc') IS NULL
                ALTER TABLE [dbo].[Reports] ADD [FinanceSubmittedAtUtc] datetimeoffset NULL;
            IF COL_LENGTH(N'dbo.Reports', N'PublishedActivityId') IS NULL
                ALTER TABLE [dbo].[Reports] ADD [PublishedActivityId] int NULL;

            IF COL_LENGTH(N'dbo.ReportUploadedFiles', N'PreviewFileName') IS NULL
                ALTER TABLE [dbo].[ReportUploadedFiles] ADD [PreviewFileName] nvarchar(260) NULL;
            IF COL_LENGTH(N'dbo.ReportUploadedFiles', N'PreviewStoragePath') IS NULL
                ALTER TABLE [dbo].[ReportUploadedFiles] ADD [PreviewStoragePath] nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.ReportUploadedFiles', N'PreviewContentType') IS NULL
                ALTER TABLE [dbo].[ReportUploadedFiles] ADD [PreviewContentType] nvarchar(120) NULL;
            IF COL_LENGTH(N'dbo.ReportUploadedFiles', N'PreviewStatus') IS NULL
                ALTER TABLE [dbo].[ReportUploadedFiles] ADD [PreviewStatus] nvarchar(40) NULL;
            IF COL_LENGTH(N'dbo.ReportUploadedFiles', N'PreviewErrorMessage') IS NULL
                ALTER TABLE [dbo].[ReportUploadedFiles] ADD [PreviewErrorMessage] nvarchar(1000) NULL;
            IF COL_LENGTH(N'dbo.ReportUploadedFiles', N'PreviewGeneratedAtUtc') IS NULL
                ALTER TABLE [dbo].[ReportUploadedFiles] ADD [PreviewGeneratedAtUtc] datetimeoffset NULL;

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Reports]')
                  AND name = N'IX_Reports_ClubId_Period_Tag'
                  AND is_unique = 1)
                DROP INDEX [IX_Reports_ClubId_Period_Tag] ON [dbo].[Reports];

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Reports]')
                  AND name = N'IX_Reports_ClubId_Period_Tag')
                CREATE INDEX [IX_Reports_ClubId_Period_Tag]
                    ON [dbo].[Reports] ([ClubId], [Period], [Tag]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Reports]')
                  AND name = N'IX_Reports_BudgetProposalId')
                EXEC(N'CREATE UNIQUE INDEX [IX_Reports_BudgetProposalId]
                    ON [dbo].[Reports] ([BudgetProposalId])
                    WHERE [BudgetProposalId] IS NOT NULL');

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

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Reports]')
                  AND name = N'IX_Reports_UpdatedAtUtc')
            BEGIN
                CREATE INDEX [IX_Reports_UpdatedAtUtc]
                    ON [dbo].[Reports] ([UpdatedAtUtc] DESC)
                    INCLUDE ([ClubId], [Period], [Status]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Reports]')
                  AND name = N'IX_Reports_CreatedByUserId')
            BEGIN
                CREATE INDEX [IX_Reports_CreatedByUserId]
                    ON [dbo].[Reports] ([CreatedByUserId], [Status]);
            END

            IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NOT NULL
               AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]')
                  AND name = N'IX_AuditLogs_ReportId')
            BEGIN
                CREATE INDEX [IX_AuditLogs_ReportId]
                    ON [dbo].[AuditLogs] ([ReportId]);
            END
            """;

        return db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
