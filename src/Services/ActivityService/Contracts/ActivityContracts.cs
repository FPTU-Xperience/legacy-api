namespace ActivityService.Contracts;

public sealed record ActivityResponse(
    int Id,
    int ClubId,
    string ClubName,
    string Title,
    string Description,
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    IReadOnlyCollection<int> MeetingDays,
    string Location,
    string Status,
    int CreatedByUserId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyCollection<ActivityParticipantResponse> Participants,
    IReadOnlyCollection<ActivityAttendanceResponse> Attendances);

public sealed record ActivityParticipantResponse(
    int Id,
    int UserId,
    string FullName,
    string AttendanceStatus,
    DateTimeOffset RegisteredAtUtc);

public sealed record ActivityAttendanceResponse(
    int Id,
    int UserId,
    string FullName,
    DateOnly AttendanceDate,
    string Status,
    string? Note,
    DateTimeOffset? CheckedInAtUtc,
    int? CheckedInByUserId);

public sealed record CreateActivityRequest(
    int ClubId,
    string ClubName,
    string Title,
    string Description,
    DateTimeOffset? StartTimeUtc,
    DateTimeOffset? EndTimeUtc,
    string Location,
    IReadOnlyCollection<int>? MeetingDays);

public sealed record CreateActivityFromApprovedReportRequest(
    int ReportId,
    int ReportDetailId,
    int ClubId,
    string ClubName,
    string Title,
    string Description,
    DateOnly ActivityDate,
    string Location);

public sealed record UpdateActivityRequest(
    string Title,
    string Description,
    DateTimeOffset? StartTimeUtc,
    DateTimeOffset? EndTimeUtc,
    string Location,
    string Status,
    IReadOnlyCollection<int>? MeetingDays);

public sealed record RegisterActivityParticipantRequest(int? UserId, string? FullName);

public sealed record MemberStatisticsInput(int UserId, DateTimeOffset JoinedAtUtc);
public sealed record MemberStatisticsQuery(IReadOnlyCollection<MemberStatisticsInput> Members);
public sealed record MemberStatisticsResponse(int UserId, int EligibleActivities, int AttendedActivities, decimal ParticipationRate);
public sealed record MemberStatisticsDetailQuery(int UserId, DateTimeOffset JoinedAtUtc, int Page = 1, int PageSize = 20);
public sealed record MemberActivityHistoryResponse(int ActivityId, string Title, DateTimeOffset StartTimeUtc, string ActivityStatus, string AttendanceStatus);
public sealed record MemberStatisticsDetailResponse(MemberStatisticsResponse Statistics, IReadOnlyCollection<MemberActivityHistoryResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public sealed record UpdateMemberAttendanceRequest(string Status, string? Note);
public sealed record BulkAttendanceItemRequest(int MemberId, string Status, string? Note);
public sealed record BulkUpdateAttendanceRequest(IReadOnlyCollection<BulkAttendanceItemRequest> Items);
public sealed record AttendanceMemberResponse(int MemberId, int UserId, string FullName, string Email, string PhoneNumber, string Role, DateTimeOffset JoinedAtUtc, string Status, string? Note, DateTimeOffset? CheckedInAtUtc, int? CheckedInByUserId);
public sealed record ActivityAttendanceManagementResponse(int ActivityId, int ClubId, string Title, DateTimeOffset StartTimeUtc, string ActivityStatus, IReadOnlyCollection<AttendanceMemberResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages, int PresentCount, int AbsentCount, int ExcusedCount, int LateCount, int NotMarkedCount);

public sealed record MyWeeklyAttendanceResponse(
    int ActivityId,
    string Title,
    IReadOnlyCollection<int> MeetingDays,
    DateOnly VietnamDate,
    bool IsScheduledToday,
    bool AlreadyCheckedInToday,
    bool CanCheckInToday,
    int ScheduledDays,
    int AttendedDays,
    decimal AttendanceRate,
    IReadOnlyCollection<ActivityAttendanceResponse> History,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
