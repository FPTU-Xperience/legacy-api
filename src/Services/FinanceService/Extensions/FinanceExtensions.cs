using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using FinanceService.Clients;

namespace FinanceService.Extensions;

public static class FinanceExtensions
{
    public static bool IsFinanceReviewer(this ClaimsPrincipal user) =>
        user.IsInRole(AuthRoles.Admin)
        || user.IsInRole(AuthRoles.StudentAffairsAdmin);

    public static bool IsCombinedReportWorkflow(this HttpContext httpContext) =>
        string.Equals(
            httpContext.Request.Headers["X-Combined-Report-Workflow"].ToString(),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static async Task<HashSet<int>> GetFinanceClubIdsAsync(
        this ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        return access.Where(x => x.CanManageFinance || x.CanManage).Select(x => x.ClubId).ToHashSet();
    }

    public static async Task<bool> CanAccessFinanceClubAsync(
        this ClubAccessClient clubAccess,
        int clubId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        return access.Any(x => x.ClubId == clubId && (x.CanManageFinance || x.CanManage));
    }

    public static async Task<bool> CanManageFinanceClubAsync(
        this ClubAccessClient clubAccess,
        int clubId,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        return access.Any(x => x.ClubId == clubId && x.CanManageFinance);
    }
}
