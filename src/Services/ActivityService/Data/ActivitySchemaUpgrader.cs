using Microsoft.EntityFrameworkCore;

namespace ActivityService.Data;

public static class ActivitySchemaUpgrader
{
    public static Task ApplyAsync(ActivityDbContext db, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[ActivityAttendances]')
                  AND name = N'IX_ActivityAttendances_UserId_AttendanceDate')
            BEGIN
                CREATE INDEX [IX_ActivityAttendances_UserId_AttendanceDate]
                    ON [dbo].[ActivityAttendances] ([UserId], [AttendanceDate])
                    INCLUDE ([ActivityId], [Status]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[ActivityParticipants]')
                  AND name = N'IX_ActivityParticipants_UserId')
            BEGIN
                CREATE INDEX [IX_ActivityParticipants_UserId]
                    ON [dbo].[ActivityParticipants] ([UserId])
                    INCLUDE ([ActivityId], [AttendanceStatus]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[Activities]')
                  AND name = N'IX_Activities_StartTimeUtc')
            BEGIN
                CREATE INDEX [IX_Activities_StartTimeUtc]
                    ON [dbo].[Activities] ([StartTimeUtc])
                    INCLUDE ([ClubId], [Title], [Status]);
            END
            """;

        return db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
