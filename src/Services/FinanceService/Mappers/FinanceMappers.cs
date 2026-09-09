using FinanceService.Contracts;
using FinanceService.Models;

namespace FinanceService.Mappers;

public static class FinanceMappers
{
    public static BudgetProposalResponse ToBudgetProposalResponse(BudgetProposal proposal) => new(
        proposal.Id,
        proposal.ClubId,
        proposal.ClubName,
        proposal.ActivityId,
        proposal.SourceReportId,
        proposal.Title,
        proposal.Description,
        proposal.RequestedAmount,
        proposal.ApprovedAmount,
        proposal.Status,
        proposal.ProposedByUserId,
        proposal.ProposedAtUtc,
        proposal.ManagerReviewedByUserId,
        proposal.ManagerReviewedAtUtc,
        proposal.ManagerReviewNote,
        proposal.ReviewedByUserId,
        proposal.ReviewedAtUtc,
        proposal.ReviewNote,
        proposal.Settlements.OrderByDescending(x => x.SubmittedAtUtc).Select(ToSettlementResponse).ToArray());

    public static SettlementResponse ToSettlementResponse(Settlement settlement) => new(
        settlement.Id,
        settlement.BudgetProposalId,
        settlement.TotalSpent,
        settlement.ReceiptUrl,
        settlement.Status,
        settlement.SubmittedAtUtc,
        settlement.ReviewedByUserId,
        settlement.ReviewedAtUtc,
        settlement.ReviewNote);

    public static FinanceTransactionResponse ToFinanceTransactionResponse(FinanceTransaction transaction) => new(
        transaction.Id,
        transaction.ClubId,
        transaction.Amount,
        transaction.Type,
        transaction.Description,
        transaction.ReferenceId,
        transaction.TransactionDateUtc);
}
