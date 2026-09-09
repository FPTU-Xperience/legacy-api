namespace ActivityService.Models;

public sealed class ClubActivity
{
    public int Id { get; set; }
    public int? SourceReportId { get; set; }
    public int? SourceReportDetailId { get; set; }
    public int ClubId { get; set; }
    public string ClubName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset StartTimeUtc { get; set; }
    public DateTimeOffset EndTimeUtc { get; set; }
    public string MeetingDaysCsv { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = ActivityStatuses.Scheduled;
    public int CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ActivityParticipant> Participants { get; set; } = [];
    public ICollection<ActivityAttendance> Attendances { get; set; } = [];
}
