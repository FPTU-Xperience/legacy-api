using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using FinanceService.Contracts;
using FinanceService.Data;
using FinanceService.Extensions;
using FinanceService.Mappers;
using FinanceService.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Endpoints;

public static class SettlementEndpoints
{
    public static void MapSettlementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/settlements", GetSettlements);
        group.MapPost("/proposals/{id:int}/settlements", CreateSettlement);
        group.MapPost("/settlements/{id:int}/approve", ApproveSettlement)
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);
    }

    private static async Task<IResult> GetSettlements(
        string? status,
        int page,
        int pageSize,
        FinanceDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;

        var baseQuery = db.Settlements.AsNoTracking();
        if (!user.IsFinanceReviewer())
        {
            var allowedClubIds = await clubAccess.GetFinanceClubIdsAsync(httpContext, cancellationToken);
            if (allowedClubIds.Count == 0)
            {
                return Results.Forbid();
            }

            baseQuery = baseQuery.Where(x => allowedClubIds.Contains(x.BudgetProposal.ClubId));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            baseQuery = baseQuery.Where(x => x.Status == status);
        }

        var total = await baseQuery.CountAsync(cancellationToken);
        var rows = await baseQuery
            .OrderByDescending(x => x.SubmittedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.BudgetProposal)
            .ToListAsync(cancellationToken);
        return Results.Ok(new { total, page, pageSize, items = rows.Select(FinanceMappers.ToSettlementResponse) });
    }

    private static async Task<IResult> CreateSettlement(
        int id,
        CreateSettlementRequest request,
        FinanceDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var proposal = await db.BudgetProposals.Include(x => x.Settlements).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (proposal is null)
        {
            return Results.NotFound();
        }

        if (!await clubAccess.CanManageFinanceClubAsync(proposal.ClubId, httpContext, cancellationToken))
        {
            return Results.Forbid();
        }

        if (proposal.Status != FinanceStatuses.Approved)
        {
            return Results.BadRequest(new { message = "Only approved budget proposals can be settled." });
        }

        if (request.TotalSpent <= 0)
        {
            return Results.BadRequest(new { message = "Total spent must be greater than zero." });
        }

        // Validate receipt URL is a valid HTTPS URL
        if (!Uri.TryCreate(request.ReceiptUrl, UriKind.Absolute, out var receiptUri))
        {
            return Results.BadRequest(new { message = "Receipt URL must be a valid URL." });
        }
        if (!receiptUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Receipt URL must use HTTPS." });
        }

        if (request.TotalSpent > 1_000_000_000)
        {
            return Results.BadRequest(new { message = "Total spent exceeds maximum allowed amount." });
        }

        // Validate settlement doesn't exceed approved budget
        if (proposal.ApprovedAmount.HasValue && request.TotalSpent > proposal.ApprovedAmount.Value)
        {
            return Results.BadRequest(new { message = $"Total spent ({request.TotalSpent:N0}) exceeds approved amount ({proposal.ApprovedAmount:N0})." });
        }

        if (proposal.Settlements.Any(x => x.Status == FinanceStatuses.Submitted || x.Status == FinanceStatuses.Approved))
        {
            return Results.Conflict(new { message = "This proposal already has an active settlement." });
        }

        var settlement = new Settlement
        {
            BudgetProposalId = id,
            TotalSpent = request.TotalSpent,
            ReceiptUrl = request.ReceiptUrl.Trim()
        };
        proposal.Settlements.Add(settlement);
        db.FinanceTransactions.Add(new FinanceTransaction
        {
            ClubId = proposal.ClubId,
            Amount = request.TotalSpent,
            Type = TransactionTypes.SettlementSubmitted,
            Description = $"Đã nộp quyết toán cho {proposal.Title}",
            ReferenceId = proposal.Id
        });
        await db.SaveChangesAsync();
        return Results.Ok(FinanceMappers.ToBudgetProposalResponse(proposal));
    }

    private static async Task<IResult> ApproveSettlement(
        int id,
        ReviewSettlementRequest request,
        FinanceDbContext db,
        ClaimsPrincipal user)
    {
        var settlement = await db.Settlements.Include(x => x.BudgetProposal).FirstOrDefaultAsync(x => x.Id == id);
        if (settlement is null)
        {
            return Results.NotFound();
        }

        if (settlement.Status != FinanceStatuses.Submitted)
        {
            return Results.BadRequest(new { message = "Only submitted settlements can be approved." });
        }

        if (settlement.BudgetProposal.ProposedByUserId == user.GetUserId())
        {
            return Results.BadRequest(new { message = "The proposal creator cannot approve its settlement." });
        }

        settlement.Status = FinanceStatuses.Approved;
        settlement.ReviewedByUserId = user.GetUserId();
        settlement.ReviewedAtUtc = DateTimeOffset.UtcNow;
        settlement.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? "Quyết toán đã được phê duyệt." : request.Note.Trim();
        settlement.BudgetProposal.Status = FinanceStatuses.Settled;
        db.FinanceTransactions.Add(new FinanceTransaction
        {
            ClubId = settlement.BudgetProposal.ClubId,
            Amount = settlement.TotalSpent,
            Type = TransactionTypes.SettlementApproved,
            Description = $"Đã phê duyệt quyết toán cho {settlement.BudgetProposal.Title}",
            ReferenceId = settlement.BudgetProposalId
        });
        await db.SaveChangesAsync();
        return Results.Ok(FinanceMappers.ToSettlementResponse(settlement));
    }
}
