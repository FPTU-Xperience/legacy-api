using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubService.Models;

[Table("ClubDisbandRequests")]
public sealed class ClubDisbandRequest
{
    public int Id { get; set; }

    public int ClubId { get; set; }

    [ForeignKey(nameof(ClubId))]
    public Club Club { get; set; } = null!;

    public int RequesterUserId { get; set; }

    [MaxLength(200)]
    public string RequesterName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [MaxLength(40)]
    public string Status { get; set; } = ClubDisbandStatuses.Pending;

    [MaxLength(1000)]
    public string? AdminNote { get; set; }

    public int? ReviewedByUserId { get; set; }

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public DateTimeOffset? ExecutedAtUtc { get; set; }
}
