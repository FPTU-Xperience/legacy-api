namespace FinanceService.Models;

public sealed class Settlement
{
    public int Id { get; set; }
    public int BudgetProposalId { get; set; }
    public BudgetProposal BudgetProposal { get; set; } = null!;
    public decimal TotalSpent { get; set; }
    public string ReceiptUrl { get; set; } = string.Empty;
    public string Status { get; set; } = FinanceStatuses.Submitted;
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}
