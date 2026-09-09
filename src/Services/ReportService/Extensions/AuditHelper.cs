using ReportService.Data;
using ReportService.Models;

namespace ReportService.Extensions;

public static class AuditHelper
{
    public static async Task AddAuditAsync(ReportDbContext db, int reportId, string action, int actorUserId, string description, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ReportId = reportId,
            Action = action,
            ActorUserId = actorUserId,
            Description = description
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
