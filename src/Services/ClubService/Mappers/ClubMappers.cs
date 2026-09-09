using System.Text.Json;
using ClubService.Contracts;
using ClubService.Models;

namespace ClubService.Mappers;

public static class ClubMappers
{
    // ========================================================================
    // Club Mappers
    // ========================================================================

    public static ClubResponse ToResponse(Club club)
    {
        var managers = club.ManagerAssignments
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.AssignedAtUtc)
            .Select(x => new ManagerAssignmentResponse(
                x.Id,
                x.ManagerUserId,
                x.ManagerName,
                x.AssignedAtUtc,
                x.EndedAtUtc,
                x.IsActive))
            .ToArray();

        var members = club.Memberships
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.Role == ClubMemberRoles.Treasurer)
            .ThenBy(x => x.FullName)
            .Select(x => ToMembershipResponseWithClub(x, club))
            .ToArray();

        return new ClubResponse(
            club.Id,
            club.Code,
            club.Name,
            club.Category,
            club.Description,
            club.LogoUrl,
            club.ContactEmail,
            club.ContactPhone,
            club.IsActive,
            managers,
            members);
    }

    public static ClubResponse ToDirectoryResponse(Club club)
    {
        var managers = club.ManagerAssignments
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.AssignedAtUtc)
            .Select(x => new ManagerAssignmentResponse(
                x.Id,
                x.ManagerUserId,
                x.ManagerName,
                x.AssignedAtUtc,
                x.EndedAtUtc,
                x.IsActive))
            .ToArray();

        var visibleMembers = club.Memberships
            .Where(x => x.Status == ClubMembershipStatuses.Approved)
            .OrderByDescending(x => x.Role == ClubMemberRoles.Treasurer)
            .ThenBy(x => x.FullName)
            .Select(m => new ClubMembershipResponse(
                m.Id,
                m.ClubId,
                club.Name,
                club.Category,
                m.UserId,
                m.FullName,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                m.Role,
                m.Status,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new Dictionary<string, string>(),
                false,
                false,
                null,
                m.RequestedAtUtc,
                m.ReviewedAtUtc,
                m.ReviewedByUserId))
            .ToArray();

        return new ClubResponse(
            club.Id,
            club.Code,
            club.Name,
            club.Category,
            club.Description,
            club.LogoUrl,
            club.ContactEmail,
            club.ContactPhone,
            club.IsActive,
            managers,
            visibleMembers);
    }

    // ========================================================================
    // Membership Mappers
    // ========================================================================

    public static ClubMembershipResponse ToMembershipResponse(ClubMembership membership)
    {
        return ToMembershipResponseWithClub(membership, membership.Club);
    }

    public static ClubMembershipResponse ToMembershipResponseWithClub(ClubMembership membership, Club club)
    {
        return new ClubMembershipResponse(
            membership.Id,
            membership.ClubId,
            club.Name,
            club.Category,
            membership.UserId,
            membership.FullName,
            membership.DateOfBirth,
            membership.Gender,
            membership.Email,
            membership.PhoneNumber,
            membership.Address,
            membership.Role,
            membership.Status,
            membership.RequestMessage,
            membership.PersonalInfo,
            membership.Goals,
            membership.Reason,
            membership.Hobbies,
            membership.Skills,
            membership.Expectations,
            membership.Contributions,
            DeserializeAdditionalInfo(membership.AdditionalInfoJson),
            membership.AcceptedClubRules,
            membership.CommittedToParticipate,
            membership.ReviewNote,
            membership.RequestedAtUtc,
            membership.ReviewedAtUtc,
            membership.ReviewedByUserId);
    }

    // ========================================================================
    // Application Mappers
    // ========================================================================

    public static ClubCreationApplicationResponse ToApplicationResponse(ClubCreationApplication application)
    {
        return new ClubCreationApplicationResponse(
            application.Id,
            application.RequesterUserId,
            application.RequesterName,
            application.Code,
            application.Name,
            application.Category,
            application.Description,
            application.Purpose,
            application.LogoUrl,
            application.ContactEmail,
            application.ContactPhone,
            application.FounderRole,
            application.FounderOrganization,
            application.FoundingMemberCount,
            DeserializeFoundingMembers(application.FoundingMembersJson),
            application.FoundingMembersCommitted,
            application.MainActivities,
            application.ActivityFrequency,
            application.ExpectedLocation,
            application.ExpectedSchedule,
            application.MajorEvents,
            application.VenueSupport,
            application.FundingSupport,
            application.EquipmentNeeds,
            application.AdvisorNeeded,
            application.CommittedToRules,
            application.CommittedToResponsibility,
            application.CommittedToReporting,
            application.Status,
            application.ReviewNote,
            application.ReviewConditions,
            application.ReviewerSignature,
            application.CreatedClubId,
            application.SubmittedAtUtc,
            application.ReviewedAtUtc,
            application.ReviewedByUserId);
    }

    // ========================================================================
    // Disband Request Mappers
    // ========================================================================

    public static DisbandRequestResponse ToDisbandRequestResponse(
        ClubDisbandRequest request,
        string clubName)
    {
        return new DisbandRequestResponse(
            request.Id,
            request.ClubId,
            clubName,
            request.Status,
            request.Reason,
            request.AdminNote,
            request.RequesterName,
            request.RequestedAtUtc,
            request.ReviewedAtUtc);
    }

    // ========================================================================
    // Transfer Request Mappers
    // ========================================================================

    public static TransferRequestResponse ToTransferRequestResponse(
        ClubOwnershipTransfer request,
        string clubName)
    {
        return new TransferRequestResponse(
            request.Id,
            request.ClubId,
            clubName,
            request.Status,
            request.Reason,
            request.AdminNote,
            request.CurrentOwnerName,
            request.NewOwnerName,
            request.RequestedAtUtc,
            request.ReviewedAtUtc);
    }

    // ========================================================================
    // Apply Request Mappers
    // ========================================================================

    public static void ApplyClubApplicationRequest(
        ClubCreationApplication application,
        CreateClubApplicationRequest request,
        string code)
    {
        application.RequesterName = request.FounderFullName.Trim();
        application.Code = code;
        application.Name = request.Name.Trim();
        application.Category = Extensions.ValidationExtensions.NormalizeClubCategory(request.Category);
        application.Description = request.Description.Trim();
        application.Purpose = request.Purpose.Trim();
        application.Reason = string.Empty;
        application.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        application.ContactEmail = request.FounderEmail.Trim();
        application.ContactPhone = request.FounderPhone.Trim();
        application.FounderRole = request.FounderRole.Trim();
        application.FounderOrganization = request.FounderOrganization.Trim();
        application.FoundingMemberCount = request.FoundingMemberCount;
        application.FoundingMembersJson = JsonSerializer.Serialize(request.FoundingMembers ?? []);
        application.FoundingMembersCommitted = request.FoundingMembersCommitted;
        application.MainActivities = request.MainActivities.Trim();
        application.ActivityFrequency = request.ActivityFrequency.Trim();
        application.ExpectedLocation = request.ExpectedLocation?.Trim() ?? string.Empty;
        application.ExpectedSchedule = request.ExpectedSchedule?.Trim() ?? string.Empty;
        application.MajorEvents = request.MajorEvents?.Trim() ?? string.Empty;
        application.VenueSupport = request.VenueSupport.Trim().ToUpperInvariant();
        application.FundingSupport = request.FundingSupport.Trim().ToUpperInvariant();
        application.EquipmentNeeds = request.EquipmentNeeds?.Trim() ?? string.Empty;
        application.AdvisorNeeded = request.AdvisorNeeded;
        application.CommittedToRules = request.CommittedToRules;
        application.CommittedToResponsibility = request.CommittedToResponsibility;
        application.CommittedToReporting = request.CommittedToReporting;
    }

    public static void ApplyJoinRequest(ClubMembership membership, JoinClubRequest request)
    {
        membership.FullName = request.FullName.Trim();
        membership.DateOfBirth = request.DateOfBirth;
        membership.Gender = request.Gender.Trim().ToUpperInvariant();
        membership.Email = request.Email.Trim();
        membership.PhoneNumber = request.PhoneNumber.Trim();
        membership.Address = request.Address?.Trim() ?? string.Empty;
        membership.RequestMessage = request.Message?.Trim();
        membership.PersonalInfo = string.Empty;
        membership.Goals = string.Empty;
        membership.Reason = request.Reason.Trim();
        membership.Hobbies = request.Hobbies?.Trim() ?? string.Empty;
        membership.Skills = request.Skills?.Trim() ?? string.Empty;
        membership.Expectations = request.Expectations?.Trim() ?? string.Empty;
        membership.Contributions = request.Contributions?.Trim() ?? string.Empty;
        membership.AdditionalInfoJson = JsonSerializer.Serialize(request.AdditionalInfo ?? new Dictionary<string, string>());
        membership.AcceptedClubRules = request.AcceptedClubRules;
        membership.CommittedToParticipate = request.CommittedToParticipate;
    }

    // ========================================================================
    // Private Helpers
    // ========================================================================

    private static IReadOnlyDictionary<string, string> DeserializeAdditionalInfo(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static IReadOnlyCollection<FoundingMemberResponse> DeserializeFoundingMembers(string json)
    {
        try
        {
            var members = JsonSerializer.Deserialize<List<FoundingMemberRequest>>(json) ?? [];
            return members
                .Select(member => new FoundingMemberResponse(member.FullName, member.Organization, member.Email))
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
