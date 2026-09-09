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

public static class ReportWorkflowEndpoints
{
    public static void MapReportWorkflowEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:int}/submit", SubmitReport);
        group.MapPost("/{id:int}/link-budget", LinkFutureEventBudget);
        group.MapPost("/{id:int}/review", ReviewReport);
        group.MapPost("/{id:int}/approve", ApproveReport)
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);
        group.MapPost("/{id:int}/reject", RejectReport);
    }

    private static async Task<IResult> SubmitReport(
        int id,
        ReportDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .Include(x => x.Details)
            .Include(x => x.Attachments)
            .Include(x => x.UploadedFile)
            .Include(x => x.Feedback)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        var currentUserId = user.GetUserId();
        var authorAccess = await ReportExtensions.GetAuthorAccessAsync(
            report.ClubId,
            report.Tag,
            report.ReportType,
            clubAccess,
            httpContext,
            cancellationToken);

        if (authorAccess is null)
        {
            var clubMembership = (await clubAccess.GetMyAccessAsync(
                    httpContext.GetBearerToken(),
                    cancellationToken))
                .FirstOrDefault(access => access.ClubId == report.ClubId);

            if (clubMembership is not null
                && ReportSubmissionRules.CanUseUploadedReportAuthorAccess(
                    report,
                    currentUserId,
                    clubMembership.IsApprovedMember))
            {
                authorAccess = clubMembership;
            }
        }

        var (isValid, errorMessage, isForbidden) = ReportSubmissionRules.ValidateSubmission(
            report,
            currentUserId,
            authorAccess is not null);
        if (isForbidden)
        {
            return Results.Forbid();
        }
        if (!isValid)
        {
            return Results.BadRequest(new { message = errorMessage });
        }
        if (authorAccess is null)
        {
            return Results.Forbid();
        }

        var isFutureEvent = ReportExtensions.IsFutureEventReportModel(report);
        if (isFutureEvent && (authorAccess.TreasurerUserIds?.Count ?? 0) == 0)
        {
            return Results.BadRequest(new { message = "Assign at least one club treasurer before submitting a future event report." });
        }

        report.Status = isFutureEvent
            ? ReportStatuses.AwaitingFinance
            : authorAccess.CanManage ? ReportStatuses.UnderReview : ReportStatuses.Submitted;
        report.SubmittedAtUtc = DateTimeOffset.UtcNow;
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        report.Version++;

        db.AddOutboxMessage(new ReportSubmittedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            report.Id,
            report.ClubId,
            report.ClubName,
            report.Period,
            user.GetUserId(),
            report.Status,
            !isFutureEvent && report.Status == ReportStatuses.Submitted
                ? authorAccess.ManagerUserIds.Cast<int?>().FirstOrDefault()
                : null,
            isFutureEvent ? "FinanceReview" : "Standard",
            isFutureEvent ? authorAccess.TreasurerUserIds : null), EventRoutingKeys.ReportSubmitted, httpContext.GetCorrelationId());

        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "Submit", user.GetUserId(), "Report submitted for review.", cancellationToken);

        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> LinkFutureEventBudget(
        int id,
        LinkFutureEventBudgetRequest request,
        ReportDbContext db,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports.Include(x => x.Details).Include(x => x.Attachments).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (!ReportExtensions.IsFutureEventReportModel(report))
        {
            return Results.BadRequest(new { message = "Only future event reports support linking budgets." });
        }

        if (report.Status != ReportStatuses.AwaitingFinance)
        {
            return Results.BadRequest(new { message = "This report is not currently awaiting a finance submission." });
        }

        if (request.RequestedAmount <= 0)
        {
            return Results.BadRequest(new { message = "Budget requested amount must be greater than zero." });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Results.BadRequest(new { message = "Budget description is required." });
        }

        var access = (await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken))
            .FirstOrDefault(x => x.ClubId == report.ClubId && x.CanManageFinance);
        if (access is null)
        {
            return Results.Forbid();
        }

        report.BudgetProposalId = request.BudgetProposalId;
        report.BudgetRequestedAmount = request.RequestedAmount;
        report.BudgetApprovedAmount = null;
        report.BudgetDescription = request.Description.Trim();
        report.FinanceSubmittedAtUtc = DateTimeOffset.UtcNow;
        report.Status = ReportStatuses.Submitted;
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;

        db.AddOutboxMessage(new ReportSubmittedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            report.Id,
            report.ClubId,
            report.ClubName,
            report.Period,
            httpContext.User.GetUserId(),
            report.Status,
            null,
            "ManagerReview",
            access.ManagerUserIds), EventRoutingKeys.ReportSubmitted, httpContext.GetCorrelationId());

        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "FinanceLinked", httpContext.User.GetUserId(), "Treasurer submitted the linked event budget.", cancellationToken);

        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> ReviewReport(
        int id,
        ReportDbContext db,
        FinanceWorkflowClient financeWorkflow,
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

        if (report.Status != ReportStatuses.Submitted)
        {
            return Results.BadRequest(new { message = "Only submitted reports can enter review." });
        }

        var isFutureEvent = ReportExtensions.IsFutureEventReportModel(report);
        // A manager may approve their own event content only when reviewing the budget
        // independently submitted by the club treasurer.
        if (!isFutureEvent && report.CreatedByUserId == user.GetUserId())
        {
            return Results.Forbid();
        }

        // Must have club management permission
        if (!await ReportExtensions.CanManageClubAsync(report.ClubId, clubAccess, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        if (isFutureEvent)
        {
            if (!report.BudgetProposalId.HasValue || !report.BudgetRequestedAmount.HasValue)
            {
                return Results.BadRequest(new { message = "The treasurer must submit the event budget before club owner review." });
            }

            var finance = await financeWorkflow.ManagerApproveAsync(
                report.BudgetProposalId.Value,
                "Approved with the combined future event report.",
                httpContext.GetBearerToken(),
                cancellationToken);
            if (finance is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        report.Status = ReportStatuses.UnderReview;
        report.ReviewedByUserId = user.GetUserId();
        report.ReviewedAtUtc = DateTimeOffset.UtcNow;
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        report.Version++;

        db.AddOutboxMessage(new ReportSubmittedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            report.Id,
            report.ClubId,
            report.ClubName,
            report.Period,
            user.GetUserId(),
            report.Status,
            null,
            isFutureEvent ? "FinalReview" : "Standard"), EventRoutingKeys.ReportSubmitted, httpContext.GetCorrelationId());

        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "ManagerReview", user.GetUserId(), "Report forwarded to Student Affairs for final approval.", cancellationToken);

        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> ApproveReport(
        int id,
        ReviewRequest request,
        ReportDbContext db,
        FinanceWorkflowClient financeWorkflow,
        ActivityPublishingClient activityPublishing,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var report = await db.Reports.Include(x => x.Details).Include(x => x.Attachments).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.Status != ReportStatuses.UnderReview)
        {
            return Results.BadRequest(new { message = "Only reports forwarded by the club manager can be approved." });
        }

        if (ReportExtensions.IsFutureEventReportModel(report))
        {
            if (!report.BudgetProposalId.HasValue || !report.BudgetRequestedAmount.HasValue || report.Details.Count != 1)
            {
                return Results.BadRequest(new { message = "The future event package is incomplete." });
            }

            var finance = await financeWorkflow.FinalApproveAsync(
                report.BudgetProposalId.Value,
                report.BudgetRequestedAmount.Value,
                "Approved with the combined future event report.",
                httpContext.GetBearerToken(),
                cancellationToken);
            if (finance is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var detail = report.Details.Single();
            var published = await activityPublishing.PublishAsync(new
            {
                reportId = report.Id,
                reportDetailId = detail.Id,
                report.ClubId,
                report.ClubName,
                title = detail.ActivityName,
                detail.Description,
                detail.ActivityDate,
                location = string.IsNullOrWhiteSpace(detail.Location) ? "To be announced" : detail.Location
            }, httpContext.GetBearerToken(), cancellationToken);
            if (published is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            report.BudgetApprovedAmount = finance.ApprovedAmount ?? report.BudgetRequestedAmount;
            report.PublishedActivityId = published.Id;
        }

        report.Status = ReportStatuses.Approved;
        report.ReviewedByUserId = user.GetUserId();
        report.ReviewedAtUtc = DateTimeOffset.UtcNow;
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        report.Version++;
        report.Feedback.Add(new ReportFeedback
        {
            ReviewerUserId = user.GetUserId(),
            ReviewerName = user.GetDisplayName(),
            Decision = ReportStatuses.Approved,
            Message = string.IsNullOrWhiteSpace(request.Feedback) ? "Báo cáo đã được phê duyệt." : request.Feedback.Trim()
        });

        db.AddOutboxMessage(new ReportApprovedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            report.Id,
            report.ClubId,
            report.ClubName,
            report.Period,
            user.GetUserId(),
            report.CreatedByUserId), EventRoutingKeys.ReportApproved, httpContext.GetCorrelationId());

        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "Approve", user.GetUserId(), "Report approved.", cancellationToken);

        return Results.Ok(ReportMappers.ToResponse(report));
    }

    private static async Task<IResult> RejectReport(
        int id,
        ReviewRequest request,
        ReportDbContext db,
        FinanceWorkflowClient financeWorkflow,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var feedback = string.IsNullOrWhiteSpace(request.Feedback) ? "Please provide the missing information and resubmit the report." : request.Feedback.Trim();
        var report = await db.Reports.Include(x => x.Details).Include(x => x.Attachments).Include(x => x.Feedback).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (report is null)
        {
            return Results.NotFound();
        }

        if (report.Status is not (ReportStatuses.Submitted or ReportStatuses.UnderReview))
        {
            return Results.BadRequest(new { message = "Only submitted or under-review reports can be rejected." });
        }

        var canReject = report.Status == ReportStatuses.Submitted
            ? report.CreatedByUserId != user.GetUserId()
                && await ReportExtensions.CanManageClubAsync(report.ClubId, clubAccess, httpContext, cancellationToken)
            : ReportExtensions.IsReportReviewer(user);
        if (!canReject)
        {
            return Results.Forbid();
        }

        if (ReportExtensions.IsFutureEventReportModel(report) && report.BudgetProposalId.HasValue)
        {
            var finance = report.Status == ReportStatuses.Submitted
                ? await financeWorkflow.ManagerRejectAsync(
                    report.BudgetProposalId.Value,
                    feedback,
                    httpContext.GetBearerToken(),
                    cancellationToken)
                : await financeWorkflow.FinalRejectAsync(
                    report.BudgetProposalId.Value,
                    feedback,
                    httpContext.GetBearerToken(),
                    cancellationToken);
            if (finance is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            report.BudgetProposalId = null;
            report.BudgetRequestedAmount = null;
            report.BudgetApprovedAmount = null;
            report.BudgetDescription = null;
            report.FinanceSubmittedAtUtc = null;
        }

        report.Status = ReportStatuses.Rejected;
        report.ReviewedByUserId = user.GetUserId();
        report.ReviewedAtUtc = DateTimeOffset.UtcNow;
        report.UpdatedAtUtc = DateTimeOffset.UtcNow;
        report.Version++;
        report.Feedback.Add(new ReportFeedback
        {
            ReviewerUserId = user.GetUserId(),
            ReviewerName = user.GetDisplayName(),
            Decision = ReportStatuses.Rejected,
            Message = feedback
        });

        db.AddOutboxMessage(new ReportRejectedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            report.Id,
            report.ClubId,
            report.ClubName,
            report.Period,
            user.GetUserId(),
            report.CreatedByUserId,
            feedback), EventRoutingKeys.ReportRejected, httpContext.GetCorrelationId());

        await db.SaveChangesAsync(cancellationToken);
        await AuditHelper.AddAuditAsync(db, report.Id, "Reject", user.GetUserId(), feedback, cancellationToken);

        return Results.Ok(ReportMappers.ToResponse(report));
    }
}
