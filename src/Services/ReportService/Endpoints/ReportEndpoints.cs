using ClubReportHub.Shared.Auth;

namespace ReportService.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        var reports = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        reports.MapReportQueryEndpoints();
        reports.MapReportCrudEndpoints();
        reports.MapReportFileEndpoints();
        reports.MapReportWorkflowEndpoints();
    }
}
