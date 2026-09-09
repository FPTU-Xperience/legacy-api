using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ClubService.Contracts;
using ClubService.Data;
using ClubService.Extensions;
using ClubService.Infrastructure;
using ClubService.Mappers;
using ClubService.Models;
using ClubService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubService.Endpoints;

public static class MemberManagementEndpoints
{
    public static void MapMemberManagementEndpoints(this WebApplication app)
    {
        var clubs = app.MapGroup("/api/clubs")
            .WithTags("Member Management")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        // GET /api/clubs/{clubId}/members - List members with pagination
        clubs.MapGet("/{clubId:int}/members", ListClubMembers)
            .WithName("ListClubMembers")
            .WithDescription("List all members of a club with pagination and filtering");

        // GET /api/clubs/{clubId}/members/{memberId} - Get member details
        clubs.MapGet("/{clubId:int}/members/{memberId:int}", GetMemberDetails)
            .WithName("GetMemberDetails")
            .WithDescription("Get detailed information about a specific member");

        // DELETE /api/clubs/{clubId}/members/{memberId} - Remove member from club
        clubs.MapDelete("/{clubId:int}/members/{memberId:int}", RemoveMember)
            .WithName("RemoveMember")
            .WithDescription("Remove a member from the club");

        // GET /api/clubs/{clubId}/member-roster - Get member roster
        clubs.MapGet("/{clubId:int}/member-roster", GetMemberRoster)
            .WithName("GetMemberRoster")
            .WithDescription("Get member roster for a club");

        // POST /api/clubs/{clubId}/member-roster/resolve - Resolve roster members
        clubs.MapPost("/{clubId:int}/member-roster/resolve", ResolveRosterMembers)
            .WithName("ResolveRosterMembers")
            .WithDescription("Resolve specific members from the roster");
    }

    private static async Task<IResult> ListClubMembers(
        int clubId,
        string? search,
        string? status,
        string? role,
        string? sortBy,
        string? sortDirection,
        int page,
        int pageSize,
        ClubDbContext db,
        ActivityStatisticsClient statisticsClient,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!await db.CanManageMembershipsAsync(clubId, user, cancellationToken))
            return Results.Forbid();

        if (!await db.Clubs.AnyAsync(x => x.Id == clubId, cancellationToken))
            return Results.NotFound(new { message = "Club not found." });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = ClubMemberQuery.ApplyFilters(
            db.ClubMemberships.AsNoTracking().Where(x => x.ClubId == clubId),
            search, status, role);
        query = ClubMemberQuery.ApplySort(query, sortBy, sortDirection);

        var totalItems = await query.CountAsync(cancellationToken);
        var memberships = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var memberUserIds = memberships.Select(x => x.UserId).ToArray();
        var activeManagerUserIds = await db.ClubManagerAssignments.AsNoTracking()
            .Where(x => x.ClubId == clubId && x.IsActive && memberUserIds.Contains(x.ManagerUserId))
            .Select(x => x.ManagerUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        IReadOnlyDictionary<int, ActivityMemberStatistics> statistics;
        try
        {
            statistics = await statisticsClient.GetBatchAsync(
                clubId,
                memberships.Select(x => new ActivityMemberInput(x.UserId, x.ReviewedAtUtc ?? x.RequestedAtUtc)).ToArray(),
                httpContext.GetBearerToken(),
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return Results.Problem(
                $"Activity statistics are temporarily unavailable: {exception.Message}",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var items = memberships.Select(x =>
        {
            var item = statistics.GetValueOrDefault(x.UserId, new ActivityMemberStatistics(x.UserId, 0, 0, 0));
            return new ClubMemberListItemResponse(
                x.Id, x.ClubId, x.UserId, x.FullName, x.Email, x.PhoneNumber,
                ClubMemberRoleRules.ResolveDisplayRole(x.Role, activeManagerUserIds.Contains(x.UserId)),
                x.Status,
                x.ReviewedAtUtc ?? x.RequestedAtUtc,
                new MemberParticipationResponse(item.EligibleActivities, item.AttendedActivities, item.ParticipationRate));
        }).ToArray();

        return Results.Ok(new PagedClubMembersResponse(
            items, page, pageSize, totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    private static async Task<IResult> GetMemberDetails(
        int clubId,
        int memberId,
        int historyPage,
        int historyPageSize,
        ClubDbContext db,
        ActivityStatisticsClient statisticsClient,
        ClaimsPrincipal user,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!await db.CanManageMembershipsAsync(clubId, user, cancellationToken))
            return Results.Forbid();

        var membership = await db.ClubMemberships.AsNoTracking()
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == memberId && x.ClubId == clubId, cancellationToken);

        if (membership is null)
            return Results.NotFound(new { message = "Member not found in this club." });

        historyPage = Math.Max(1, historyPage);
        historyPageSize = Math.Clamp(historyPageSize, 1, 100);

        ActivityStatisticsDetail detail;
        try
        {
            detail = await statisticsClient.GetDetailAsync(
                clubId,
                new ActivityStatisticsDetailQuery(
                    membership.UserId,
                    membership.ReviewedAtUtc ?? membership.RequestedAtUtc,
                    historyPage,
                    historyPageSize),
                httpContext.GetBearerToken(),
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return Results.Problem(
                $"Activity statistics are temporarily unavailable: {exception.Message}",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new ClubMemberDetailResponse(
            ClubMappers.ToMembershipResponse(membership),
            membership.ReviewedAtUtc ?? membership.RequestedAtUtc,
            new MemberParticipationResponse(
                detail.Statistics.EligibleActivities,
                detail.Statistics.AttendedActivities,
                detail.Statistics.ParticipationRate),
            detail.Items.Select(x => new MemberActivityHistoryItemResponse(
                x.ActivityId, x.Title, x.StartTimeUtc, x.ActivityStatus, x.AttendanceStatus)).ToArray(),
            detail.Page, detail.PageSize, detail.TotalItems, detail.TotalPages));
    }

    private static async Task<IResult> RemoveMember(
        int clubId,
        int memberId,
        ClubDbContext db,
        ClaimsPrincipal user,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (!await db.CanManageMembershipsAsync(clubId, user, cancellationToken))
            return Results.Forbid();

        var membership = await db.ClubMemberships
            .FirstOrDefaultAsync(x => x.Id == memberId && x.ClubId == clubId, cancellationToken);

        if (membership is null)
            return Results.NotFound(new { message = "Member not found in this club." });

        var isActiveManager = await db.ClubManagerAssignments.AsNoTracking()
            .AnyAsync(x => x.ClubId == clubId && x.ManagerUserId == membership.UserId && x.IsActive, cancellationToken);

        var removalError = ClubMemberRoleRules.ValidateRemoval(isActiveManager);
        if (removalError is not null)
            return Results.Conflict(new { message = removalError });

        membership.IsDeleted = true;
        membership.DeletedAtUtc = DateTimeOffset.UtcNow;
        membership.DeletedByUserId = user.GetUserId();
        membership.Status = ClubMembershipStatuses.Inactive;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Membership {MembershipId} was soft-deleted from club {ClubId} by user {UserId}",
            memberId, clubId, user.GetUserId());

        return Results.NoContent();
    }

    private static async Task<IResult> GetMemberRoster(
        int clubId,
        DateTimeOffset joinedOnOrBefore,
        string? search,
        int page,
        int pageSize,
        ClubDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!await db.CanManageMembershipsAsync(clubId, user, cancellationToken))
            return Results.Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ClubMemberships.AsNoTracking()
            .Where(x => x.ClubId == clubId
                && x.Status == ClubMembershipStatuses.Approved
                && (x.ReviewedAtUtc ?? x.RequestedAtUtc) <= joinedOnOrBefore);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.FullName.Contains(term) || x.Email.Contains(term) || x.PhoneNumber.Contains(term));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ClubMemberRosterItemResponse(
                x.Id, x.UserId, x.FullName, x.Email, x.PhoneNumber,
                x.Role, x.Status, x.ReviewedAtUtc ?? x.RequestedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedClubMemberRosterResponse(
            rows, page, pageSize, totalItems,
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    private static async Task<IResult> ResolveRosterMembers(
        int clubId,
        ResolveClubMemberRosterRequest request,
        ClubDbContext db,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!await db.CanManageMembershipsAsync(clubId, user, cancellationToken))
            return Results.Forbid();

        if (request.MemberIds is null
            || request.MemberIds.Count is < 1 or > 500
            || request.MemberIds.Distinct().Count() != request.MemberIds.Count)
        {
            return Results.BadRequest(new { message = "Provide between 1 and 500 unique member IDs." });
        }

        var cutoff = request.JoinedOnOrBefore ?? DateTimeOffset.MaxValue;
        var rows = await db.ClubMemberships.AsNoTracking()
            .Where(x => x.ClubId == clubId
                && request.MemberIds.Contains(x.Id)
                && x.Status == ClubMembershipStatuses.Approved
                && (x.ReviewedAtUtc ?? x.RequestedAtUtc) <= cutoff)
            .OrderBy(x => x.FullName)
            .Select(x => new ClubMemberRosterItemResponse(
                x.Id, x.UserId, x.FullName, x.Email, x.PhoneNumber,
                x.Role, x.Status, x.ReviewedAtUtc ?? x.RequestedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(rows);
    }
}
