using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using FinanceService.Data;
using FinanceService.Extensions;
using FinanceService.Mappers;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Endpoints;

public static class TransactionEndpoints
{
    public static void MapTransactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transactions", GetTransactions);
    }

    private static async Task<IResult> GetTransactions(
        int? clubId,
        FinanceDbContext db,
        ClaimsPrincipal user,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var query = db.FinanceTransactions.AsNoTracking();
        if (!user.IsFinanceReviewer())
        {
            var allowedClubIds = await clubAccess.GetFinanceClubIdsAsync(httpContext, cancellationToken);
            if (allowedClubIds.Count == 0)
            {
                return Results.Forbid();
            }

            if (clubId.HasValue && !allowedClubIds.Contains(clubId.Value))
            {
                return Results.Forbid();
            }

            query = query.Where(x => allowedClubIds.Contains(x.ClubId));
        }

        if (clubId.HasValue)
        {
            query = query.Where(x => x.ClubId == clubId);
        }

        var rows = await query.OrderByDescending(x => x.TransactionDateUtc).Take(100).ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(FinanceMappers.ToFinanceTransactionResponse));
    }
}
