using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubService.Models;

[Table("ClubOwnershipTransfers")]
public sealed class ClubOwnershipTransfer
{
    public int Id { get; set; }

    public int ClubId { get; set; }

    [ForeignKey(nameof(ClubId))]
    public Club Club { get; set; } = null!;

    public int CurrentOwnerUserId { get; set; }

    [MaxLength(200)]
    public string CurrentOwnerName { get; set; } = string.Empty;

    public int NewOwnerUserId { get; set; }

    [MaxLength(200)]
    public string NewOwnerName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [MaxLength(40)]
    public string Status { get; set; } = ClubOwnershipTransferStatuses.Pending;

    [MaxLength(1000)]
    public string? AdminNote { get; set; }

    public int? ReviewedByUserId { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public DateTimeOffset? ExecutedAtUtc { get; set; }
}
