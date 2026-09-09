using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Data;
using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Messaging;
using ClubReportHub.Shared.Tracing;
using Microsoft.EntityFrameworkCore;
using ReportService.Clients;
using ReportService.Contracts;
using ReportService.Data;
using ReportService.Extensions;
using ReportService.Models;
using ReportService.Services;

namespace ReportService.Endpoints;

public static class ReportCrudEndpoints
{
    public static void MapReportCrudEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateReport);
        group.MapPut("/{id:int}", UpdateReport);
    }

    private static async Task<IResult> CreateReport(
        CreateReportRequest request,
        ReportDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var tag = ReportExtensions.NormalizeReportTag(request.Tag, request.ReportType);
        var reportType = ReportExtensions.NormalizeReportType(request.ReportType, tag);
        var isFutureEvent = ReportExtensions.IsFutureEventReport(reportType);
        var authorAccess = await ReportExtensions.GetAuthorAccessAsync(
            request.ClubId,
            tag,
            reportType,
            clubAccess,
            httpContext,
            cancellationToken);
        if (authorAccess is null)
        {
            return Results.Forbid();
        }

        var period = request.Period.Trim();

        if (!isFutureEvent
            && await db.Reports.AnyAsync(x => x.ClubId == request.ClubId && x.Period == period && x.Tag == tag, cancellationToken))
        {
            return Results.Conflict(new { message = "A report already exists for this club, period, and tag." });
        }

        var futureValidation = ReportExtensions.ValidateFutureEventDetails(reportType, request.Details);
        if (futureValidation is not null)
        {
            return Results.BadRequest(new { message = futureValidation });
        }

        var deadline = await db.ReportingDeadlines.FirstOrDefaultAsync(x => x.Period == period, cancellationToken);
        var dueDate = deadline?.DueDate ?? request.DueDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));

        var report = new Report
        {
            ClubId = request.ClubId,
            ClubName = authorAccess.ClubName,
            Period = period,
            ReportType = reportType,
            Tag = tag,
            DueDate = dueDate,
            CreatedByUserId = user.GetUserId(),
            ExecutiveSummary = request.ExecutiveSummary?.Trim(),
            Achievements = request.Achievements?.Trim(),
            Challenges = request.Challenges?.Trim(),
            Recommendations = request.Recommendations?.Trim(),
            NextPeriodPlan = request.NextPeriodPlan?.Trim(),
            Details = request.Details.Select(detail => ReportExtensions.ToDetail(detail, includeBudget: !isFutureEvent)).ToList()
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(cancellationToken);

        var recipientUserIds = authorAccess.ManagerUserIds
            .Append(user.GetUserId())
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        db.AddOutboxMessage(new ReportCreatedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            report.Id,
            report.ClubId,
            report.ClubName,
            report.Period,
            report.CreatedByUserId,
            recipientUserIds), EventRoutingKeys.ReportCreated, httpContext.GetCorrelationId());

        await AuditHelper.AddAuditAsync(db, report.Id, "Create", user.GetUserId(), "Report draft created.", cancellationToken);

        return Results.Created($"/api/reports/{report.Id}", ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> UpdateReport(
        int id,
        UpdateReportRequest request,
        ReportDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports.Include(x => x.Details).Include(x => x.Attachments).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.Status is not (ReportStatuses.Draft or ReportStatuses.Rejected))
        {
            return Results.BadRequest(new { message = "Only draft or rejected reports can be edited." });
        }

        var period = request.Period.Trim();
        var tag = ReportExtensions.NormalizeReportTag(request.Tag, request.ReportType);
        var reportType = ReportExtensions.NormalizeReportType(request.ReportType, tag);
        var isFutureEvent = ReportExtensions.IsFutureEventReport(reportType);
        if (report.CreatedByUserId != user.GetUserId()
            || !await ReportExtensions.CanAuthorReportsAsync(
                report.ClubId,
                tag,
                reportType,
                clubAccess,
                httpContext,
                cancellationToken))
        {
            return Results.Forbid();
        }

        if (!isFutureEvent
            && await db.Reports.AnyAsync(x => x.Id != id && x.ClubId == report.ClubId && x.Period == period && x.Tag == tag))
        {
            return Results.Conflict(new { message = "Another report already uses this club, period, and tag." });
        }

        var futureValidation = ReportExtensions.ValidateFutureEventDetails(reportType, request.Details);
        if (futureValidation is not null)
        {
            return Results.BadRequest(new { message = futureValidation });
        }

        var deadline = await db.ReportingDeadlines.FirstOrDefaultAsync(x => x.Period == period, cancellationToken);
        if (deadline is not null)
        {
            report.DueDate = deadline.DueDate;
        }
        else if (request.DueDate.HasValue)
        {
            report.DueDate = request.DueDate.Value;
        }

        report.Period = period;
        report.Tag = tag;
        report.ReportType = reportType;
        report.ExecutiveSummary = request.ExecutiveSummary?.Trim();
        report.Achievements = request.Achievements?.Trim();
        report.Challenges = request.Challenges?.Trim();
        report.Recommendations = request.Recommendations?.Trim();
        report.NextPeriodPlan = request.NextPeriodPlan?.Trim();
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        report.Version++;
        db.ReportDetails.RemoveRange(report.Details);
        report.Details = request.Details.Select(detail => ReportExtensions.ToDetail(detail, includeBudget: !isFutureEvent)).ToList();
        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "Update", user.GetUserId(), "Report draft updated.", cancellationToken);
        return Results.Ok(ReportMappers.ToResponse(report));
    }
}
