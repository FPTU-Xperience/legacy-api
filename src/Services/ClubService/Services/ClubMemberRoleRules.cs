using ClubService.Models;

namespace ClubService.Services;

public static class ClubMemberRoleRules
{
    public const string ClubOwner = "CLUB_OWNER";

    public static string ResolveDisplayRole(string membershipRole, bool isActiveManager) =>
        isActiveManager ? ClubOwner : membershipRole;

    public static string? ValidateTreasurerAssignment(bool isActiveManager, bool isAlreadyTreasurer, int treasurerCount)
    {
        if (isActiveManager)
        {
            return "A club owner cannot be assigned as treasurer.";
        }

        return !isAlreadyTreasurer && treasurerCount >= 2
            ? "A club can have at most two treasurers."
            : null;
    }

    public static string? ValidateRemoval(bool isActiveManager) =>
        isActiveManager ? "A club owner cannot be removed from the member roster." : null;

    public static void ApplyTreasurerRole(ClubMembership membership, string? memberName)
    {
        if (!string.IsNullOrWhiteSpace(memberName))
        {
            membership.FullName = memberName.Trim();
        }

        membership.Role = ClubMemberRoles.Treasurer;
    }

    public static void ApplyMemberRole(ClubMembership membership) =>
        membership.Role = ClubMemberRoles.Member;
}
