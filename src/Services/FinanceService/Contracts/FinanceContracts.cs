using System.ComponentModel.DataAnnotations;

namespace FinanceService.Contracts;

public sealed record BudgetProposalResponse(
    int Id,
    int ClubId,
    string ClubName,
    int? ActivityId,
    int? SourceReportId,
    string Title,
    string Description,
    decimal RequestedAmount,
    decimal? ApprovedAmount,
    string Status,
    int ProposedByUserId,
    DateTimeOffset ProposedAtUtc,
    int? ManagerReviewedByUserId,
    DateTimeOffset? ManagerReviewedAtUtc,
    string? ManagerReviewNote,
    int? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewNote,
    IReadOnlyCollection<SettlementResponse> Settlements);

public sealed record SettlementResponse(
    int Id,
    int BudgetProposalId,
    decimal TotalSpent,
    string ReceiptUrl,
    string Status,
    DateTimeOffset SubmittedAtUtc,
    int? ReviewedByUserId,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewNote);

public sealed record FinanceTransactionResponse(
    int Id,
    int ClubId,
    decimal Amount,
    string Type,
    string Description,
    int? ReferenceId,
    DateTimeOffset TransactionDateUtc);

public sealed record CreateBudgetProposalRequest(
    int ClubId,
    [StringLength(200)] string ClubName,
    int? ActivityId,
    [StringLength(200)] string Title,
    [StringLength(1000)] string Description,
    [Range(0.01, 1_000_000_000)] decimal RequestedAmount,
    int? SourceReportId = null);

public sealed record ReviewBudgetProposalRequest(
    [Range(0.01, 1_000_000_000)] decimal? ApprovedAmount,
    [StringLength(1000)] string? Note);

public sealed record CreateSettlementRequest(
    [Range(0.01, 1_000_000_000)] decimal TotalSpent,
    [StringLength(500), Url] string ReceiptUrl);

public sealed record ReviewSettlementRequest([StringLength(1000)] string? Note);
