namespace FinanceService.Models;

public sealed class FinanceTransaction
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? ReferenceId { get; set; }
    public DateTimeOffset TransactionDateUtc { get; set; } = DateTimeOffset.UtcNow;
}
