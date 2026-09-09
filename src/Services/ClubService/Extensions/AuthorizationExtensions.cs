using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ClubService.Data;
using Microsoft.EntityFrameworkCore;

namespace ClubService.Extensions;

public static class AuthorizationExtensions
{
    public static bool IsStudentAffairsAdministrator(this ClaimsPrincipal user)
    {
        return user.IsInRole(AuthRoles.Admin)
            || user.IsInRole(AuthRoles.StudentAffairsAdmin);
    }

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
        => user.IsInRole(AuthRoles.Admin);

    public static async Task<bool> UserOwnsClubAsync(
        this ClubDbContext db,
        int clubId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await db.ClubManagerAssignments
            .AnyAsync(x => x.ClubId == clubId && x.ManagerUserId == userId && x.IsActive, cancellationToken);
    }

    public static async Task<bool> CanManageMembershipsAsync(
        this ClubDbContext db,
        int clubId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        if (user.IsStudentAffairsAdministrator())
            return true;

        var userId = user.GetUserId();
        return await db.ClubManagerAssignments.AsNoTracking()
            .AnyAsync(x => x.ClubId == clubId && x.ManagerUserId == userId && x.IsActive, cancellationToken);
    }

    public static async Task<bool> IsClubOwnerAsync(
        this ClubDbContext db,
        int clubId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await db.ClubManagerAssignments
            .AnyAsync(x => x.ClubId == clubId && x.ManagerUserId == userId && x.IsActive, cancellationToken);
    }
}
