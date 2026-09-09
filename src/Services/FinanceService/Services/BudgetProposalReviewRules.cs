using FinanceService.Models;

namespace FinanceService.Services;

public static class BudgetProposalReviewRules
{
    public static bool CanManagerReview(string? status) =>
        string.Equals(status, FinanceStatuses.Submitted, StringComparison.OrdinalIgnoreCase);

    public static bool CanFinalReview(string? status) =>
        string.Equals(status, FinanceStatuses.ManagerApproved, StringComparison.OrdinalIgnoreCase);
}
