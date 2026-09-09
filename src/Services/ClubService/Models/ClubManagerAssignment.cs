namespace ClubService.Models;

public sealed class ClubManagerAssignment
{
    public int Id { get; set; }
    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;
    public int ManagerUserId { get; set; }
    public string ManagerName { get; set; } = string.Empty;
    public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
