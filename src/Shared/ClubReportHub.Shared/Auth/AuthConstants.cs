namespace ClubReportHub.Shared.Auth;

public static class AuthRoles
{
    public const string Admin = "ADMIN";
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string StudentAffairsAdmin = "STUDENT_AFFAIRS_ADMIN";
    public const string ClubManager = "CLUB_MANAGER";
    public const string Treasurer = "TREASURER";
    public const string ClubMember = "CLUB_MEMBER";

    public static bool IsKnown(string? role) =>
        role is Admin
            or SystemAdmin
            or StudentAffairsAdmin
            or ClubManager
            or Treasurer
            or ClubMember;
}

public static class AuthPolicies
{
    public const string AllActors = "AllActors";
    public const string BusinessAccess = "BusinessAccess";
    public const string SystemAdministration = "SystemAdministration";
    public const string StudentAffairsAdministration = "StudentAffairsAdministration";
    public const string SuperAdminOnly = "SuperAdminOnly";
    public const string AdminOnly = "AdminOnly";
    public const string ClubManagerOnly = "ClubManagerOnly";
    public const string TreasurerOnly = "TreasurerOnly";
    public const string ClubMemberOnly = "ClubMemberOnly";
    public const string AdminOrClubManager = "AdminOrClubManager";
    public const string AdminOrClubManagerOrMember = "AdminOrClubManagerOrMember";
    public const string ClubManagerOrTreasurer = "ClubManagerOrTreasurer";
}
