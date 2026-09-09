using System.ComponentModel.DataAnnotations;

namespace ClubService.Contracts;

public sealed record ClubResponse(
    int Id,
    string Code,
    string Name,
    string Category,
    string Description,
    string? LogoUrl,
    string ContactEmail,
    string ContactPhone,
    bool IsActive,
    IReadOnlyCollection<ManagerAssignmentResponse> Managers,
    IReadOnlyCollection<ClubMembershipResponse> Members);

public sealed record ManagerAssignmentResponse(
    int Id,
    int ManagerUserId,
    string ManagerName,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? EndedAtUtc,
    bool IsActive);

public sealed record ClubMembershipResponse(
    int Id,
    int ClubId,
    string ClubName,
    string ClubCategory,
    int UserId,
    string FullName,
    DateOnly? DateOfBirth,
    string Gender,
    string Email,
    string PhoneNumber,
    string Address,
    string Role,
    string Status,
    string? RequestMessage,
    string PersonalInfo,
    string Goals,
    string Reason,
    string Hobbies,
    string Skills,
    string Expectations,
    string Contributions,
    IReadOnlyDictionary<string, string> AdditionalInfo,
    bool AcceptedClubRules,
    bool CommittedToParticipate,
    string? ReviewNote,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    int? ReviewedByUserId);

public sealed record ClubAccessResponse(
    int ClubId,
    string ClubName,
    bool IsManager,
    bool IsTreasurer,
    bool IsApprovedMember,
    IReadOnlyCollection<int> ManagerUserIds,
    IReadOnlyCollection<int> MemberUserIds,
    IReadOnlyCollection<int> TreasurerUserIds);

public sealed record FoundingMemberResponse(string FullName, string Organization, string Email);

public sealed record ClubCreationApplicationResponse(
    int Id,
    int RequesterUserId,
    string RequesterName,
    string Code,
    string Name,
    string Category,
    string Description,
    string Purpose,
    string? LogoUrl,
    string ContactEmail,
    string ContactPhone,
    string FounderRole,
    string FounderOrganization,
    int FoundingMemberCount,
    IReadOnlyCollection<FoundingMemberResponse> FoundingMembers,
    bool FoundingMembersCommitted,
    string MainActivities,
    string ActivityFrequency,
    string ExpectedLocation,
    string ExpectedSchedule,
    string MajorEvents,
    string VenueSupport,
    string FundingSupport,
    string EquipmentNeeds,
    bool AdvisorNeeded,
    bool CommittedToRules,
    bool CommittedToResponsibility,
    bool CommittedToReporting,
    string Status,
    string? ReviewNote,
    string? ReviewConditions,
    string? ReviewerSignature,
    int? CreatedClubId,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    int? ReviewedByUserId);

public sealed record CreateClubRequest(
    [StringLength(20)] string Code,
    [StringLength(200)] string Name,
    [StringLength(1000)] string Description,
    [StringLength(255), EmailAddress] string ContactEmail,
    [StringLength(20)] string ContactPhone,
    [StringLength(40)] string? Category = null,
    [StringLength(1000)] string? LogoUrl = null);

public sealed record UpdateClubRequest(
    [StringLength(200)] string Name,
    [StringLength(1000)] string Description,
    [StringLength(255), EmailAddress] string ContactEmail,
    [StringLength(20)] string ContactPhone,
    bool IsActive,
    [StringLength(40)] string? Category = null,
    [StringLength(1000)] string? LogoUrl = null);

public sealed record AssignManagerRequest(
    int ManagerUserId,
    [StringLength(200)] string ManagerName);

public sealed record JoinClubRequest(
    [StringLength(200)] string FullName,
    DateOnly? DateOfBirth,
    [StringLength(20)] string Gender,
    [StringLength(255), EmailAddress] string Email,
    [StringLength(40)] string PhoneNumber,
    [StringLength(500)] string? Address,
    [StringLength(1000)] string? Hobbies,
    [StringLength(1000)] string? Skills,
    [StringLength(1000)] string Reason,
    [StringLength(1000)] string? Expectations,
    [StringLength(1000)] string? Contributions,
    IReadOnlyDictionary<string, string>? AdditionalInfo,
    bool AcceptedClubRules,
    bool CommittedToParticipate,
    [StringLength(1000)] string? Message);

public sealed record ReviewClubMembershipRequest([StringLength(1000)] string? Note);

public sealed record AssignTreasurerRequest(
    int MemberUserId,
    [StringLength(200)] string MemberName);

public sealed record FoundingMemberRequest(
    [StringLength(200)] string FullName,
    [StringLength(300)] string Organization,
    [StringLength(255), EmailAddress] string Email);

public sealed record CreateClubApplicationRequest(
    [StringLength(20)] string? Code,
    [StringLength(200)] string Name,
    [StringLength(40)] string Category,
    [StringLength(1000)] string Purpose,
    [StringLength(1000)] string Description,
    [StringLength(1000)] string? LogoUrl,
    [StringLength(200)] string FounderFullName,
    [StringLength(200)] string FounderRole,
    [StringLength(255), EmailAddress] string FounderEmail,
    [StringLength(40)] string FounderPhone,
    [StringLength(300)] string FounderOrganization,
    [Range(1, 200)] int FoundingMemberCount,
    IReadOnlyCollection<FoundingMemberRequest> FoundingMembers,
    bool FoundingMembersCommitted,
    [StringLength(2000)] string MainActivities,
    [StringLength(200)] string ActivityFrequency,
    [StringLength(500)] string? ExpectedLocation,
    [StringLength(500)] string? ExpectedSchedule,
    [StringLength(2000)] string? MajorEvents,
    [StringLength(40)] string VenueSupport,
    [StringLength(40)] string FundingSupport,
    [StringLength(1000)] string? EquipmentNeeds,
    bool AdvisorNeeded,
    bool CommittedToRules,
    bool CommittedToResponsibility,
    bool CommittedToReporting);

public sealed record ReviewClubApplicationRequest(
    [StringLength(1000)] string? Note,
    [StringLength(1000)] string? Conditions = null,
    [StringLength(200)] string? ReviewerSignature = null);

// ============================================================================
// CLUB DISBAND CONTRACTS
// ============================================================================

public sealed record DisbandClubRequest(
    [Required, MaxLength(1000)] string? Reason
);

public sealed record DisbandRequestResponse(
    int Id,
    int ClubId,
    string ClubName,
    string Status,
    string? Reason,
    string? AdminNote,
    string RequesterName,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ReviewedAtUtc
);

public sealed record ApproveDisbandRequest(
    [Required, MaxLength(1000)] string? AdminNote
);

// ============================================================================
// CLUB OWNERSHIP TRANSFER CONTRACTS
// ============================================================================

public sealed record TransferOwnershipRequest(
    [Required] int NewOwnerUserId,
    [MaxLength(1000)] string? Reason
);

public sealed record TransferRequestResponse(
    int Id,
    int ClubId,
    string ClubName,
    string Status,
    string? Reason,
    string? AdminNote,
    string CurrentOwnerName,
    string NewOwnerName,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? ReviewedAtUtc
);

public sealed record ApproveTransferRequest(
    [Required, MaxLength(1000)] string? AdminNote
);

public sealed record ClubMemberForTransferResponse(
    int UserId,
    string FullName,
    string Email,
    string Role,
    DateTimeOffset JoinedAtUtc
);

public sealed record MemberParticipationResponse(int EligibleActivities, int AttendedActivities, decimal ParticipationRate);
public sealed record ClubMemberListItemResponse(int Id, int ClubId, int UserId, string FullName, string Email, string PhoneNumber, string Role, string Status, DateTimeOffset JoinedAtUtc, MemberParticipationResponse Participation);
public sealed record PagedClubMembersResponse(IReadOnlyCollection<ClubMemberListItemResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record MemberActivityHistoryItemResponse(int ActivityId, string Title, DateTimeOffset StartTimeUtc, string ActivityStatus, string AttendanceStatus);
public sealed record ClubMemberDetailResponse(ClubMembershipResponse Member, DateTimeOffset JoinedAtUtc, MemberParticipationResponse Participation, IReadOnlyCollection<MemberActivityHistoryItemResponse> ActivityHistory, int HistoryPage, int HistoryPageSize, int HistoryTotalItems, int HistoryTotalPages);
public sealed record ClubMemberRosterItemResponse(int Id, int UserId, string FullName, string Email, string PhoneNumber, string Role, string Status, DateTimeOffset JoinedAtUtc);
public sealed record PagedClubMemberRosterResponse(IReadOnlyCollection<ClubMemberRosterItemResponse> Items, int Page, int PageSize, int TotalItems, int TotalPages);
public sealed record ResolveClubMemberRosterRequest(IReadOnlyCollection<int> MemberIds, DateTimeOffset? JoinedOnOrBefore);
