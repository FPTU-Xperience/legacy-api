namespace ClubService.Models;

public static class ClubMembershipStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Inactive = "Inactive";
}

public static class ClubMemberRoles
{
    public const string Member = "MEMBER";
    public const string Treasurer = "TREASURER";
}

public static class ClubApplicationStatuses
{
    public const string Submitted = "Submitted";
    public const string NeedsRevision = "NeedsRevision";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class ClubCategories
{
    public const string Sports = "SPORTS";
    public const string Arts = "ARTS";
    public const string Academic = "ACADEMIC";
    public const string Volunteer = "VOLUNTEER";
    public const string Technology = "TECHNOLOGY";
    public const string Other = "OTHER";

    public static readonly string[] All = [Sports, Arts, Academic, Volunteer, Technology, Other];
}

public static class ClubGenders
{
    public const string Male = "MALE";
    public const string Female = "FEMALE";
    public const string Other = "OTHER";

    public static readonly string[] All = [Male, Female, Other];
}

public static class ClubResourceOptions
{
    public const string SupportNeeded = "SUPPORT_NEEDED";
    public const string SelfManaged = "SELF_MANAGED";

    public static readonly string[] All = [SupportNeeded, SelfManaged];
}

public static class ClubFundingOptions
{
    public const string SupportNeeded = "SUPPORT_NEEDED";
    public const string SelfFunded = "SELF_FUNDED";
    public const string Combined = "COMBINED";

    public static readonly string[] All = [SupportNeeded, SelfFunded, Combined];
}

// ============================================================================
// CLUB DISBAND STATUSES
// ============================================================================
public static class ClubDisbandStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

// ============================================================================
// CLUB OWNERSHIP TRANSFER STATUSES
// ============================================================================
public static class ClubOwnershipTransferStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
