using System.Security.Claims;

namespace ClubReportHub.Shared.Auth;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User ID claim not found in token.");

        if (!int.TryParse(value, out var userId) || userId <= 0)
        {
            throw new UnauthorizedAccessException("Invalid user ID format in token.");
        }

        return userId;
    }

    public static string GetDisplayName(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue("username")
            ?? "System";
    }
}
