using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ClubService.Contracts;
using ClubService.Data;
using ClubService.Extensions;
using ClubService.Mappers;
using ClubService.Models;
using ClubService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubService.Endpoints;

public static class MembershipEndpoints
{
    public static void MapMembershipEndpoints(this WebApplication app)
    {
        var clubs = app.MapGroup("/api/clubs")
            .WithTags("Memberships")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        // POST /api/clubs/{id}/join - Join a club
        clubs.MapPost("/{id:int}/join", JoinClub)
            .WithName("JoinClub")
            .WithDescription("Request to join a club");

        // GET /api/clubs/{id}/memberships - Get club memberships
        clubs.MapGet("/{id:int}/memberships", GetClubMemberships)
            .WithName("GetClubMemberships")
            .WithDescription("Get all memberships for a club");

        // POST /api/clubs/memberships/{membershipId}/approve - Approve membership
        clubs.MapPost("/memberships/{membershipId:int}/approve", ApproveMembership)
            .WithName("ApproveMembership")
            .WithDescription("Approve a membership request");

        // POST /api/clubs/memberships/{membershipId}/reject - Reject membership
        clubs.MapPost("/memberships/{membershipId:int}/reject", RejectMembership)
            .WithName("RejectMembership")
            .WithDescription("Reject a membership request");

        // POST /api/clubs/{id}/treasurers - Assign treasurer
        clubs.MapPost("/{id:int}/treasurers", AssignTreasurer)
            .WithName("AssignTreasurer")
            .WithDescription("Assign a member as club treasurer");

        // POST /api/clubs/memberships/{membershipId}/member - Remove treasurer role
        clubs.MapPost("/memberships/{membershipId:int}/member", RemoveTreasurerRole)
            .WithName("RemoveTreasurerRole")
            .WithDescription("Remove treasurer role from a member");

        // DELETE /api/clubs/memberships/{membershipId} - Remove membership
        clubs.MapDelete("/memberships/{membershipId:int}", RemoveMembership)
            .WithName("RemoveMembership")
            .WithDescription("Remove a member from club");
    }

    private static async Task<IResult> JoinClub(
        int id,
        JoinClubRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        var club = await db.Clubs
            .Include(x => x.ManagerAssignments)
            .Include(x => x.Memberships)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (club is null)
        {
            return Results.NotFound();
        }

        var userId = user.GetUserId();
        if (club.ManagerAssignments.Any(x => x.ManagerUserId == userId && x.IsActive))
        {
            return Results.Conflict(new { message = "Club owner is already attached to this club." });
        }

        var validationError = ValidationExtensions.ValidateJoinRequest(request, club.Category);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var existing = await db.ClubMemberships.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.ClubId == id && x.UserId == userId);

        if (existing is not null)
        {
            if (existing.Status == ClubMembershipStatuses.Rejected || existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAtUtc = null;
                existing.DeletedByUserId = null;
                existing.Status = ClubMembershipStatuses.Pending;
                existing.Role = ClubMemberRoles.Member;
                ClubMappers.ApplyJoinRequest(existing, request);
                existing.ReviewNote = null;
                existing.RequestedAtUtc = DateTimeOffset.UtcNow;
                existing.ReviewedAtUtc = null;
                existing.ReviewedByUserId = null;
                await db.SaveChangesAsync();
                return Results.Ok(ClubMappers.ToMembershipResponseWithClub(existing, club));
            }

            return Results.Conflict(new { message = $"Membership request already exists with status {existing.Status}." });
        }

        var membership = new ClubMembership
        {
            ClubId = id,
            UserId = userId,
            Role = ClubMemberRoles.Member,
            Status = ClubMembershipStatuses.Pending
        };

        ClubMappers.ApplyJoinRequest(membership, request);
        db.ClubMemberships.Add(membership);
        await db.SaveChangesAsync();

        membership.Club = club;
        return Results.Created($"/api/clubs/memberships/{membership.Id}", ClubMappers.ToMembershipResponse(membership));
    }

    private static async Task<IResult> GetClubMemberships(
        int id,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        if (!user.IsSuperAdmin() && !await db.UserOwnsClubAsync(id, user.GetUserId()))
        {
            return Results.Forbid();
        }

        var memberships = await db.ClubMemberships
            .Include(x => x.Club)
            .Where(x => x.ClubId == id)
            .OrderBy(x => x.Status)
            .ThenBy(x => x.FullName)
            .ToListAsync();

        return Results.Ok(memberships.Select(ClubMappers.ToMembershipResponse));
    }

    private static async Task<IResult> ApproveMembership(
        int membershipId,
        ReviewClubMembershipRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        if (request.Note?.Trim().Length > 1000)
        {
            return Results.BadRequest(new { message = "The review note cannot exceed 1,000 characters." });
        }

        var membership = await db.ClubMemberships
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == membershipId);

        if (membership is null)
        {
            return Results.NotFound();
        }

        if (!user.IsSuperAdmin() && !await db.UserOwnsClubAsync(membership.ClubId, user.GetUserId()))
        {
            return Results.Forbid();
        }

        if (membership.Status != ClubMembershipStatuses.Pending)
        {
            return Results.Conflict(new { message = "Only pending membership requests can be approved." });
        }

        membership.Status = ClubMembershipStatuses.Approved;
        membership.Role = ClubMemberRoles.Member;
        membership.ReviewNote = request.Note?.Trim();
        membership.ReviewedAtUtc = DateTimeOffset.UtcNow;
        membership.ReviewedByUserId = user.GetUserId();

        await db.SaveChangesAsync();
        return Results.Ok(ClubMappers.ToMembershipResponse(membership));
    }

    private static async Task<IResult> RejectMembership(
        int membershipId,
        ReviewClubMembershipRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        if (request.Note?.Trim().Length > 1000)
        {
            return Results.BadRequest(new { message = "The review note cannot exceed 1,000 characters." });
        }

        var membership = await db.ClubMemberships
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == membershipId);

        if (membership is null)
        {
            return Results.NotFound();
        }

        if (!user.IsSuperAdmin() && !await db.UserOwnsClubAsync(membership.ClubId, user.GetUserId()))
        {
            return Results.Forbid();
        }

        if (membership.Status != ClubMembershipStatuses.Pending)
        {
            return Results.Conflict(new { message = "Only pending membership requests can be rejected." });
        }

        membership.Status = ClubMembershipStatuses.Rejected;
        membership.Role = ClubMemberRoles.Member;
        membership.ReviewNote = request.Note?.Trim();
        membership.ReviewedAtUtc = DateTimeOffset.UtcNow;
        membership.ReviewedByUserId = user.GetUserId();

        await db.SaveChangesAsync();
        return Results.Ok(ClubMappers.ToMembershipResponse(membership));
    }

    private static async Task<IResult> AssignTreasurer(
        int id,
        AssignTreasurerRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        if (!user.IsSuperAdmin() && !await db.UserOwnsClubAsync(id, user.GetUserId()))
        {
            return Results.Forbid();
        }

        var club = await db.Clubs
            .Include(x => x.Memberships)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (club is null)
        {
            return Results.NotFound();
        }

        var membership = club.Memberships.FirstOrDefault(x => x.UserId == request.MemberUserId);
        if (membership is null || membership.Status != ClubMembershipStatuses.Approved)
        {
            return Results.BadRequest(new { message = "Treasurer must be an approved member of this club." });
        }

        var isActiveManager = await db.ClubManagerAssignments.AsNoTracking()
            .AnyAsync(x => x.ClubId == id && x.ManagerUserId == membership.UserId && x.IsActive);

        var treasurerCount = club.Memberships.Count(x => x.Role == ClubMemberRoles.Treasurer && x.Status == ClubMembershipStatuses.Approved);

        var assignmentError = ClubMemberRoleRules.ValidateTreasurerAssignment(
            isActiveManager,
            membership.Role == ClubMemberRoles.Treasurer,
            treasurerCount);

        if (assignmentError is not null)
        {
            return Results.Conflict(new { message = assignmentError });
        }

        ClubMemberRoleRules.ApplyTreasurerRole(membership, request.MemberName);
        await db.SaveChangesAsync();

        membership.Club = club;
        return Results.Ok(ClubMappers.ToMembershipResponse(membership));
    }

    private static async Task<IResult> RemoveTreasurerRole(
        int membershipId,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        var membership = await db.ClubMemberships
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == membershipId);

        if (membership is null)
        {
            return Results.NotFound();
        }

        if (!user.IsSuperAdmin() && !await db.UserOwnsClubAsync(membership.ClubId, user.GetUserId()))
        {
            return Results.Forbid();
        }

        ClubMemberRoleRules.ApplyMemberRole(membership);
        await db.SaveChangesAsync();

        return Results.Ok(ClubMappers.ToMembershipResponse(membership));
    }

    private static async Task<IResult> RemoveMembership(
        int membershipId,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        var membership = await db.ClubMemberships
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == membershipId);

        if (membership is null)
        {
            return Results.NotFound();
        }

        if (!user.IsSuperAdmin() && !await db.UserOwnsClubAsync(membership.ClubId, user.GetUserId()))
        {
            return Results.Forbid();
        }

        membership.IsDeleted = true;
        membership.DeletedAtUtc = DateTimeOffset.UtcNow;
        membership.DeletedByUserId = user.GetUserId();
        membership.Status = ClubMembershipStatuses.Inactive;

        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
