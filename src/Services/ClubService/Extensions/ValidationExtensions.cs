using System.Net.Mail;
using System.Text.RegularExpressions;
using ClubService.Contracts;
using ClubService.Models;

namespace ClubService.Extensions;

public static class ValidationExtensions
{
    public static string? ValidateClubApplication(
        CreateClubApplicationRequest request,
        string normalizedCode)
    {
        if (string.IsNullOrWhiteSpace(normalizedCode)
            || string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Description)
            || string.IsNullOrWhiteSpace(request.Purpose)
            || string.IsNullOrWhiteSpace(request.Category)
            || string.IsNullOrWhiteSpace(request.FounderFullName)
            || string.IsNullOrWhiteSpace(request.FounderRole)
            || string.IsNullOrWhiteSpace(request.FounderEmail)
            || string.IsNullOrWhiteSpace(request.FounderPhone)
            || string.IsNullOrWhiteSpace(request.FounderOrganization)
            || string.IsNullOrWhiteSpace(request.MainActivities)
            || string.IsNullOrWhiteSpace(request.ActivityFrequency))
        {
            return "Please provide the club code, name, description, purpose, reason, and contact information.";
        }

        if (!Regex.IsMatch(normalizedCode, "^[A-Z0-9_-]{2,20}$"))
        {
            return "The club code must contain 2-20 letters, numbers, hyphens, or underscores.";
        }

        if (!MailAddress.TryCreate(request.FounderEmail.Trim(), out var email)
            || !string.Equals(email.Address, request.FounderEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "The contact email is invalid.";
        }

        if (!Regex.IsMatch(request.FounderPhone.Trim(), @"^\+?[0-9]{9,15}$"))
        {
            return "The phone number must contain 9 to 15 digits.";
        }

        if (request.Name.Trim().Length > 150
            || request.Description.Trim().Length > 1000
            || request.Purpose.Trim().Length > 1000
            || request.MainActivities.Trim().Length > 2000)
        {
            return "The application content exceeds the allowed length.";
        }

        if (!ClubCategories.All.Contains(request.Category.Trim().ToUpperInvariant()))
        {
            return "The club category is invalid.";
        }

        if (!ClubResourceOptions.All.Contains(request.VenueSupport?.Trim().ToUpperInvariant())
            || !ClubFundingOptions.All.Contains(request.FundingSupport?.Trim().ToUpperInvariant()))
        {
            return "The venue or funding option is invalid.";
        }

        if (request.FoundingMembers is null
            || request.FoundingMemberCount < 1
            || request.FoundingMemberCount < request.FoundingMembers.Count)
        {
            return "The number of founding members must be valid and cannot be less than the submitted member list.";
        }

        if (request.FoundingMembers.Any(member =>
            string.IsNullOrWhiteSpace(member.FullName)
            || string.IsNullOrWhiteSpace(member.Organization)
            || !MailAddress.TryCreate(member.Email?.Trim(), out _)))
        {
            return "The founding member list contains invalid information.";
        }

        if (!request.FoundingMembersCommitted
            || !request.CommittedToRules
            || !request.CommittedToResponsibility
            || !request.CommittedToReporting)
        {
            return "You must accept all commitments before submitting the application.";
        }

        return null;
    }

    public static string? ValidateJoinRequest(JoinClubRequest request, string clubCategory)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)
            || request.DateOfBirth is null
            || string.IsNullOrWhiteSpace(request.Gender)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.Reason))
        {
            return "Please provide your personal information, objectives, and reason for joining the club.";
        }

        if ((request.Message?.Trim().Length ?? 0) > 1000
            || request.FullName.Trim().Length > 200
            || request.Reason.Trim().Length > 1000
            || (request.Hobbies?.Trim().Length ?? 0) > 1000
            || (request.Skills?.Trim().Length ?? 0) > 1000
            || (request.Expectations?.Trim().Length ?? 0) > 1000
            || (request.Contributions?.Trim().Length ?? 0) > 1000)
        {
            return "The membership application exceeds the allowed length.";
        }

        var dateOfBirth = request.DateOfBirth.Value;
        if (dateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow)
            || dateOfBirth < DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-100)))
        {
            return "The date of birth is invalid.";
        }

        if (!ClubGenders.All.Contains(request.Gender.Trim().ToUpperInvariant()))
        {
            return "The gender value is invalid.";
        }

        if (!MailAddress.TryCreate(request.Email.Trim(), out _))
        {
            return "The email address is invalid.";
        }

        if (!Regex.IsMatch(request.PhoneNumber.Trim(), @"^\+?[0-9]{9,15}$"))
        {
            return "The phone number must contain 9 to 15 digits.";
        }

        if (!request.AcceptedClubRules || !request.CommittedToParticipate)
        {
            return "You must accept both club participation commitments.";
        }

        var allowedAdditionalFields = GetAdditionalFieldKeys(clubCategory);
        if (request.AdditionalInfo is { Count: > 0 }
            && request.AdditionalInfo.Any(item =>
                !allowedAdditionalFields.Contains(item.Key)
                || item.Value?.Trim().Length > 1000))
        {
            return "The additional information does not match the club category.";
        }

        return null;
    }

    public static string NormalizeClubCategory(string? category)
    {
        var normalized = category?.Trim().ToUpperInvariant();
        return ClubCategories.All.Contains(normalized) ? normalized! : ClubCategories.Other;
    }

    public static string NormalizeOrGenerateClubCode(string? code, int requesterUserId)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            return code.Trim().ToUpperInvariant();
        }

        var suffix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000;
        return $"CLB-{requesterUserId}-{suffix}";
    }

    public static HashSet<string> GetAdditionalFieldKeys(string category)
    {
        return NormalizeClubCategory(category) switch
        {
            ClubCategories.Sports => ["sport", "level", "experience"],
            ClubCategories.Arts => ["artField", "level"],
            ClubCategories.Academic => ["academicInterest", "learningGoal"],
            ClubCategories.Volunteer => ["volunteerInterest", "socialWorkExperience"],
            ClubCategories.Technology => ["programmingLanguages", "projects"],
            _ => ["other"]
        };
    }

    public static string? ValidateReviewContent(string? note, string? conditions, string? signature)
    {
        if ((note?.Trim().Length ?? 0) > 1000
            || (conditions?.Trim().Length ?? 0) > 1000
            || (signature?.Trim().Length ?? 0) > 200)
        {
            return "The review content exceeds the allowed length.";
        }
        return null;
    }
}
