using ClubReportHub.Shared.Auth;

namespace FinanceService.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this WebApplication app)
    {
        var finance = app.MapGroup("/api/finance")
            .WithTags("Finance")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        finance.MapProposalEndpoints();
        finance.MapSettlementEndpoints();
        finance.MapTransactionEndpoints();
    }
}
