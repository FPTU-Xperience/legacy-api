using Hangfire.Dashboard;
using ClubReportHub.Shared.Auth;

namespace ReportService.Jobs;

/// <summary>
/// Authorizes access to the Hangfire Dashboard.
/// Only authenticated users with Admin or SystemAdmin role are granted access.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return user.IsInRole(AuthRoles.Admin) || user.IsInRole(AuthRoles.SystemAdmin);
    }
}

/// <summary>
/// Alias kept for backwards compatibility.
/// </summary>
[Obsolete("Use HangfireDashboardAuthorizationFilter instead.")]
public sealed class AllowAllDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly HangfireDashboardAuthorizationFilter _inner = new();

    public bool Authorize(DashboardContext context) => _inner.Authorize(context);
}
