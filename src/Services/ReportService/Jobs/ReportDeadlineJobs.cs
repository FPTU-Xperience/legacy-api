using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Messaging;
using Microsoft.EntityFrameworkCore;
using ReportService.Data;

namespace ReportService.Jobs;

public sealed class ReportDeadlineJobs(ReportDbContext db, IEventBus eventBus, ILogger<ReportDeadlineJobs> logger)
{
    public async Task PublishDailyReminderAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var reminders = await db.ReportingDeadlines
            .Where(x => x.IsActive && x.DueDate >= today && x.DueDate <= today.AddDays(3))
            .ToListAsync(cancellationToken);

        foreach (var deadline in reminders)
        {
            await PublishReminderForPeriod(deadline.Period, deadline.DueDate, cancellationToken);
        }
    }

    public async Task PublishMissingReportCheckAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var overdue = await db.ReportingDeadlines
            .Where(x => x.IsActive && x.DueDate < today)
            .OrderByDescending(x => x.DueDate)
            .Take(3)
            .ToListAsync(cancellationToken);

        foreach (var deadline in overdue)
        {
            await PublishReminderForPeriod(deadline.Period, deadline.DueDate, cancellationToken);
        }
    }

    private async Task PublishReminderForPeriod(string period, DateOnly dueDate, CancellationToken cancellationToken)
    {
        var submittedClubIds = await db.Reports
            .Where(x => x.Period == period && x.Status != "Draft")
            .Select(x => x.ClubId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // NOTE: The complete list of active clubs is owned by ClubService.
        // This bounded context only knows about clubs that have submitted reports.
        // The NotificationService should cross-reference with ClubService to determine truly missing clubs.
        // For now, we emit all club IDs that have NOT submitted, letting NotificationService filter.
        IReadOnlyCollection<int> missingClubIds = submittedClubIds.Count == 0
            ? Array.Empty<int>()
            : await db.Reports
                .Where(x => x.Period != period && submittedClubIds.Contains(x.ClubId))
                .Select(x => x.ClubId)
                .Distinct()
                .ToListAsync(cancellationToken);

        await eventBus.PublishAsync(new ReportDeadlineReminderEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            period,
            dueDate,
            missingClubIds.ToArray()), EventRoutingKeys.ReportDeadlineReminder, cancellationToken);

        logger.LogInformation("Published deadline reminder for {Period} due {DueDate} with {MissingCount} clubs that may need reminder",
            period, dueDate, missingClubIds.Count);
    }
}
