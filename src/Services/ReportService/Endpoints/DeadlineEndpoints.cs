using ClubReportHub.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using ReportService.Clients;
using ReportService.Contracts;
using ReportService.Data;
using ReportService.Models;

namespace ReportService.Endpoints;

public static class DeadlineEndpoints
{
    public static void MapDeadlineEndpoints(this WebApplication app)
    {
        var deadlines = app.MapGroup("/api/deadlines")
            .WithTags("Deadlines")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        deadlines.MapGet("/", GetDeadlines);
        deadlines.MapPost("/", CreateOrUpdateDeadline);

        // Manager-accessible deadline endpoint
        app.MapGet("/api/deadlines/me", GetMyDeadlines)
            .WithTags("Deadlines")
            .RequireAuthorization(AuthPolicies.BusinessAccess);
    }

    private static async Task<IResult> GetDeadlines(ReportDbContext db)
    {
        var deadlines = await db.ReportingDeadlines.OrderBy(x => x.Period).ToListAsync();
        return Results.Ok(deadlines);
    }

    private static async Task<IResult> CreateOrUpdateDeadline(DeadlineRequest request, ReportDbContext db)
    {
        var deadline = await db.ReportingDeadlines.FirstOrDefaultAsync(x => x.Period == request.Period);
        if (deadline is null)
        {
            deadline = new ReportingDeadline { Period = request.Period.Trim() };
            db.ReportingDeadlines.Add(deadline);
        }

        deadline.DueDate = request.DueDate;
        deadline.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return Results.Ok(deadline);
    }

    private static async Task<IResult> GetMyDeadlines(
        ReportDbContext db,
        HttpContext httpContext,
        ClubAccessClient clubAccess,
        CancellationToken cancellationToken)
    {
        var access = await clubAccess.GetMyAccessAsync(httpContext.GetBearerToken(), cancellationToken);
        if (!access.Any(x => x.IsManager))
        {
            return Results.Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await db.ReportingDeadlines
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DueDate)
            .Select(x => new MyDeadlineResponse(
                x.Id,
                x.Period,
                x.DueDate,
                x.DueDate < today,
                x.DueDate.DayNumber - today.DayNumber))
            .ToListAsync(cancellationToken);
        return Results.Ok(rows);
    }
}
