using Microsoft.EntityFrameworkCore;

namespace ClubService.Data;

public static class ClubSchemaUpgrader
{
    public static Task ApplyAsync(ClubDbContext db, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[ClubMemberships]')
                  AND name = N'IX_ClubMemberships_UserId_Status')
            BEGIN
                CREATE INDEX [IX_ClubMemberships_UserId_Status]
                    ON [dbo].[ClubMemberships] ([UserId], [Status])
                    INCLUDE ([ClubId], [Role], [IsDeleted]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Clubs]')
                  AND name = N'IX_Clubs_IsActive')
            BEGIN
                CREATE INDEX [IX_Clubs_IsActive]
                    ON [dbo].[Clubs] ([IsActive])
                    INCLUDE ([Code], [Name], [Category]);
            END

            IF OBJECT_ID(N'[dbo].[ClubDisbandRequests]', N'U') IS NOT NULL
               AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[ClubDisbandRequests]')
                  AND name = N'IX_ClubDisbandRequests_ClubId')
            BEGIN
                CREATE INDEX [IX_ClubDisbandRequests_ClubId]
                    ON [dbo].[ClubDisbandRequests] ([ClubId]);
            END

            IF OBJECT_ID(N'[dbo].[ClubOwnershipTransfers]', N'U') IS NOT NULL
               AND NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[ClubOwnershipTransfers]')
                  AND name = N'IX_ClubOwnershipTransfers_ClubId')
            BEGIN
                CREATE INDEX [IX_ClubOwnershipTransfers_ClubId]
                    ON [dbo].[ClubOwnershipTransfers] ([ClubId]);
            END
            """;

        return db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
