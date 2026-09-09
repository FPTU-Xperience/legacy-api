namespace ClubService.Models;

public sealed class ClubCreationApplication
{
    public int Id { get; set; }
    public int RequesterUserId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = ClubCategories.Other;
    public string Description { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string FounderRole { get; set; } = string.Empty;
    public string FounderOrganization { get; set; } = string.Empty;
    public int FoundingMemberCount { get; set; }
    public string FoundingMembersJson { get; set; } = "[]";
    public bool FoundingMembersCommitted { get; set; }
    public string MainActivities { get; set; } = string.Empty;
    public string ActivityFrequency { get; set; } = string.Empty;
    public string ExpectedLocation { get; set; } = string.Empty;
    public string ExpectedSchedule { get; set; } = string.Empty;
    public string MajorEvents { get; set; } = string.Empty;
    public string VenueSupport { get; set; } = ClubResourceOptions.SelfManaged;
    public string FundingSupport { get; set; } = ClubFundingOptions.SelfFunded;
    public string EquipmentNeeds { get; set; } = string.Empty;
    public bool AdvisorNeeded { get; set; }
    public bool CommittedToRules { get; set; }
    public bool CommittedToResponsibility { get; set; }
    public bool CommittedToReporting { get; set; }
    public string Status { get; set; } = ClubApplicationStatuses.Submitted;
    public string? ReviewNote { get; set; }
    public string? ReviewConditions { get; set; }
    public string? ReviewerSignature { get; set; }
    public int? CreatedClubId { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public int? ReviewedByUserId { get; set; }
}
