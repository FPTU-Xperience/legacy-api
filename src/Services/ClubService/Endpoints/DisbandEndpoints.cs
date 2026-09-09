using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ClubService.Contracts;
using ClubService.Data;
using ClubService.Extensions;
using ClubService.Mappers;
using ClubService.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubService.Endpoints;

/// <summary>
/// Endpoints for club disband workflow.
/// Owner sends disband request -> Admin reviews -> Approved/Rejected
/// </summary>
public static class DisbandEndpoints
{
    public static void MapDisbandEndpoints(this WebApplication app)
    {
        var clubs = app.MapGroup("/api/clubs")
            .WithTags("Club Disband")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        // Owner: Submit disband request
        clubs.MapPost("/{clubId:int}/disband", SubmitDisbandRequest)
            .WithName("SubmitDisbandRequest")
            .WithDescription("Submit a club disband request (Club owner only)");

        // Owner: Get disband request for their club
        clubs.MapGet("/{clubId:int}/disband-request", GetDisbandRequest)
            .WithName("GetDisbandRequest")
            .WithDescription("Get disband request for a club (Club owner only)");

        // Admin: Get all disband requests
        clubs.MapGet("/disband-requests", GetAllDisbandRequests)
            .WithName("GetAllDisbandRequests")
            .WithDescription("Get all pending disband requests (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        // Admin: Approve disband request
        clubs.MapPost("/disband-requests/{requestId:int}/approve", ApproveDisbandRequest)
            .WithName("ApproveDisbandRequest")
            .WithDescription("Approve a club disband request (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        // Admin: Reject disband request
        clubs.MapPost("/disband-requests/{requestId:int}/reject", RejectDisbandRequest)
            .WithName("RejectDisbandRequest")
            .WithDescription("Reject a club disband request (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);
    }

    /// <summary>
    /// Owner: Submit a disband request for their club.
    /// </summary>
    private static async Task<IResult> SubmitDisbandRequest(
        int clubId,
        DisbandClubRequest request,
        ClubDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var userId = http.User.GetUserId();
        var userName = http.User.GetDisplayName();

        var club = await db.Clubs.FindAsync(new object[] { clubId }, ct);
        if (club is null)
            return Results.NotFound(new { message = "Club not found" });

        if (!club.IsActive)
            return Results.BadRequest(new { message = "Club is already inactive" });

        var isOwner = await db.ClubManagerAssignments.AnyAsync(
            x => x.ClubId == clubId && x.ManagerUserId == userId && x.IsActive, ct);

        if (!isOwner)
            return Results.Forbid();

        var existingRequest = await db.ClubDisbandRequests
            .AnyAsync(x => x.ClubId == clubId && x.Status == ClubDisbandStatuses.Pending, ct);

        if (existingRequest)
            return Results.BadRequest(new { message = "A pending disband request already exists for this club" });

        var disbandRequest = new ClubDisbandRequest
        {
            ClubId = clubId,
            RequesterUserId = userId,
            RequesterName = userName,
            Reason = request.Reason,
            Status = ClubDisbandStatuses.Pending,
            RequestedAtUtc = DateTimeOffset.UtcNow
        };

        db.ClubDisbandRequests.Add(disbandRequest);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/clubs/disband-requests/{disbandRequest.Id}",
            ClubMappers.ToDisbandRequestResponse(disbandRequest, club.Name));
    }

    /// <summary>
    /// Owner: Get the disband request for their club.
    /// </summary>
    private static async Task<IResult> GetDisbandRequest(
        int clubId,
        ClubDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var userId = http.User.GetUserId();

        var isOwner = await db.ClubManagerAssignments.AnyAsync(
            x => x.ClubId == clubId && x.ManagerUserId == userId && x.IsActive, ct);

        if (!isOwner)
            return Results.Forbid();

        var request = await db.ClubDisbandRequests
            .Where(x => x.ClubId == clubId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (request is null)
            return Results.Ok(new { message = "No disband request found" });

        var club = await db.Clubs.FindAsync(new object[] { clubId }, ct);

        return Results.Ok(ClubMappers.ToDisbandRequestResponse(request, club?.Name ?? string.Empty));
    }

    /// <summary>
    /// Admin: Get all disband requests.
    /// </summary>
    private static async Task<IResult> GetAllDisbandRequests(
        ClubDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        if (!http.User.IsStudentAffairsAdministrator())
            return Results.Forbid();

        var requests = await db.ClubDisbandRequests
            .Include(x => x.Club)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => ClubMappers.ToDisbandRequestResponse(x, x.Club.Name))
            .ToListAsync(ct);

        return Results.Ok(requests);
    }

    /// <summary>
    /// Admin: Approve a disband request and deactivate the club.
    /// </summary>
    private static async Task<IResult> ApproveDisbandRequest(
        int requestId,
        ApproveDisbandRequest request,
        ClubDbContext db,
        HttpContext http,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!http.User.IsStudentAffairsAdministrator())
            return Results.Forbid();

        var reviewerUserId = http.User.GetUserId();

        var disbandRequest = await db.ClubDisbandRequests
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (disbandRequest is null)
            return Results.NotFound(new { message = "Disband request not found" });

        if (disbandRequest.Status != ClubDisbandStatuses.Pending)
            return Results.BadRequest(new { message = $"Request is already {disbandRequest.Status}" });

        disbandRequest.Status = ClubDisbandStatuses.Approved;
        disbandRequest.AdminNote = request.AdminNote;
        disbandRequest.ReviewedByUserId = reviewerUserId;
        disbandRequest.ReviewedAtUtc = DateTimeOffset.UtcNow;
        disbandRequest.ExecutedAtUtc = DateTimeOffset.UtcNow;

        disbandRequest.Club.IsActive = false;
        disbandRequest.Club.DeletedAtUtc = DateTimeOffset.UtcNow;
        disbandRequest.Club.DeletedByUserId = reviewerUserId;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Club {ClubId} ({ClubName}) disbanded and approved by admin {AdminId}",
            disbandRequest.ClubId, disbandRequest.Club.Name, reviewerUserId);

        return Results.Ok(ClubMappers.ToDisbandRequestResponse(disbandRequest, disbandRequest.Club.Name));
    }

    /// <summary>
    /// Admin: Reject a disband request.
    /// </summary>
    private static async Task<IResult> RejectDisbandRequest(
        int requestId,
        ApproveDisbandRequest request,
        ClubDbContext db,
        HttpContext http,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!http.User.IsStudentAffairsAdministrator())
            return Results.Forbid();

        var reviewerUserId = http.User.GetUserId();

        var disbandRequest = await db.ClubDisbandRequests
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (disbandRequest is null)
            return Results.NotFound(new { message = "Disband request not found" });

        if (disbandRequest.Status != ClubDisbandStatuses.Pending)
            return Results.BadRequest(new { message = $"Request is already {disbandRequest.Status}" });

        disbandRequest.Status = ClubDisbandStatuses.Rejected;
        disbandRequest.AdminNote = request.AdminNote;
        disbandRequest.ReviewedByUserId = reviewerUserId;
        disbandRequest.ReviewedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Club {ClubId} disband request rejected by admin {AdminId}",
            disbandRequest.ClubId, reviewerUserId);

        return Results.Ok(ClubMappers.ToDisbandRequestResponse(disbandRequest, disbandRequest.Club.Name));
    }
}
