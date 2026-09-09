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
/// Endpoints for club ownership transfer workflow.
/// Owner selects member -> Submit transfer request -> Admin approves/rejects
/// </summary>
public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this WebApplication app)
    {
        var clubs = app.MapGroup("/api/clubs")
            .WithTags("Club Ownership Transfer")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        // Owner: Get list of approved members eligible for ownership transfer
        clubs.MapGet("/{clubId:int}/members-for-transfer", GetEligibleMembersForTransfer)
            .WithName("GetEligibleMembersForTransfer")
            .WithDescription("Get list of members eligible for ownership transfer");

        // Owner: Submit ownership transfer request
        clubs.MapPost("/{clubId:int}/transfer-ownership", SubmitTransferRequest)
            .WithName("SubmitTransferRequest")
            .WithDescription("Submit an ownership transfer request (Club owner only)");

        // Owner: Get transfer request for their club
        clubs.MapGet("/{clubId:int}/transfer-request", GetTransferRequest)
            .WithName("GetTransferRequest")
            .WithDescription("Get transfer request for a club (Club owner only)");

        // Admin: Get all transfer requests
        clubs.MapGet("/transfer-requests", GetAllTransferRequests)
            .WithName("GetAllTransferRequests")
            .WithDescription("Get all pending transfer requests (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        // Admin: Approve transfer request
        clubs.MapPost("/transfer-requests/{requestId:int}/approve", ApproveTransferRequest)
            .WithName("ApproveTransferRequest")
            .WithDescription("Approve an ownership transfer request (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        // Admin: Reject transfer request
        clubs.MapPost("/transfer-requests/{requestId:int}/reject", RejectTransferRequest)
            .WithName("RejectTransferRequest")
            .WithDescription("Reject an ownership transfer request (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);
    }

    /// <summary>
    /// Owner: Get list of approved members eligible for ownership transfer.
    /// </summary>
    private static async Task<IResult> GetEligibleMembersForTransfer(
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

        var members = await db.ClubMemberships
            .Where(x => x.ClubId == clubId && x.Status == ClubMembershipStatuses.Approved)
            .OrderBy(x => x.FullName)
            .Select(x => new ClubMemberForTransferResponse(
                x.UserId, x.FullName, x.Email, x.Role,
                x.ReviewedAtUtc ?? x.RequestedAtUtc))
            .ToListAsync(ct);

        return Results.Ok(members);
    }

    /// <summary>
    /// Owner: Submit an ownership transfer request.
    /// </summary>
    private static async Task<IResult> SubmitTransferRequest(
        int clubId,
        TransferOwnershipRequest request,
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
            return Results.BadRequest(new { message = "Club is inactive" });

        var isOwner = await db.ClubManagerAssignments.AnyAsync(
            x => x.ClubId == clubId && x.ManagerUserId == userId && x.IsActive, ct);

        if (!isOwner)
            return Results.Forbid();

        var newOwnerMembership = await db.ClubMemberships
            .FirstOrDefaultAsync(
                x => x.ClubId == clubId && x.UserId == request.NewOwnerUserId && x.Status == ClubMembershipStatuses.Approved,
                ct);

        if (newOwnerMembership is null)
            return Results.BadRequest(new { message = "New owner must be an approved club member" });

        if (request.NewOwnerUserId == userId)
            return Results.BadRequest(new { message = "Cannot transfer ownership to yourself" });

        var existingRequest = await db.ClubOwnershipTransfers
            .AnyAsync(x => x.ClubId == clubId && x.Status == ClubOwnershipTransferStatuses.Pending, ct);

        if (existingRequest)
            return Results.BadRequest(new { message = "A pending transfer request already exists for this club" });

        var transferRequest = new ClubOwnershipTransfer
        {
            ClubId = clubId,
            CurrentOwnerUserId = userId,
            CurrentOwnerName = userName,
            NewOwnerUserId = request.NewOwnerUserId,
            NewOwnerName = newOwnerMembership.FullName,
            Reason = request.Reason,
            Status = ClubOwnershipTransferStatuses.Pending,
            RequestedAtUtc = DateTimeOffset.UtcNow
        };

        db.ClubOwnershipTransfers.Add(transferRequest);
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/clubs/transfer-requests/{transferRequest.Id}",
            ClubMappers.ToTransferRequestResponse(transferRequest, club.Name));
    }

    /// <summary>
    /// Owner: Get the transfer request for their club.
    /// </summary>
    private static async Task<IResult> GetTransferRequest(
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

        var request = await db.ClubOwnershipTransfers
            .Where(x => x.ClubId == clubId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (request is null)
            return Results.Ok(new { message = "No transfer request found" });

        var club = await db.Clubs.FindAsync(new object[] { clubId }, ct);

        return Results.Ok(ClubMappers.ToTransferRequestResponse(request, club?.Name ?? string.Empty));
    }

    /// <summary>
    /// Admin: Get all transfer requests.
    /// </summary>
    private static async Task<IResult> GetAllTransferRequests(
        ClubDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        if (!http.User.IsStudentAffairsAdministrator())
            return Results.Forbid();

        var requests = await db.ClubOwnershipTransfers
            .Include(x => x.Club)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => ClubMappers.ToTransferRequestResponse(x, x.Club.Name))
            .ToListAsync(ct);

        return Results.Ok(requests);
    }

    /// <summary>
    /// Admin: Approve a transfer request and change club ownership.
    /// </summary>
    private static async Task<IResult> ApproveTransferRequest(
        int requestId,
        ApproveTransferRequest request,
        ClubDbContext db,
        HttpContext http,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!http.User.IsStudentAffairsAdministrator())
            return Results.Forbid();

        var reviewerUserId = http.User.GetUserId();

        var transferRequest = await db.ClubOwnershipTransfers
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (transferRequest is null)
            return Results.NotFound(new { message = "Transfer request not found" });

        if (transferRequest.Status != ClubOwnershipTransferStatuses.Pending)
            return Results.BadRequest(new { message = $"Request is already {transferRequest.Status}" });

        transferRequest.Status = ClubOwnershipTransferStatuses.Approved;
        transferRequest.AdminNote = request.AdminNote;
        transferRequest.ReviewedByUserId = reviewerUserId;
        transferRequest.ReviewedAtUtc = DateTimeOffset.UtcNow;
        transferRequest.ExecutedAtUtc = DateTimeOffset.UtcNow;

        // Deactivate current owner
        var currentAssignment = await db.ClubManagerAssignments
            .FirstOrDefaultAsync(x => x.ClubId == transferRequest.ClubId && x.IsActive, ct);

        if (currentAssignment != null)
        {
            currentAssignment.IsActive = false;
            currentAssignment.EndedAtUtc = DateTimeOffset.UtcNow;
        }

        // Create new owner assignment
        var newAssignment = new ClubManagerAssignment
        {
            ClubId = transferRequest.ClubId,
            ManagerUserId = transferRequest.NewOwnerUserId,
            ManagerName = transferRequest.NewOwnerName,
            AssignedAtUtc = DateTimeOffset.UtcNow,
            IsActive = true
        };
        db.ClubManagerAssignments.Add(newAssignment);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Club {ClubId} ownership transferred from {OldOwner} to {NewOwner} by admin {AdminId}",
            transferRequest.ClubId, transferRequest.CurrentOwnerUserId,
            transferRequest.NewOwnerUserId, reviewerUserId);

        return Results.Ok(ClubMappers.ToTransferRequestResponse(transferRequest, transferRequest.Club.Name));
    }

    /// <summary>
    /// Admin: Reject a transfer request.
    /// </summary>
    private static async Task<IResult> RejectTransferRequest(
        int requestId,
        ApproveTransferRequest request,
        ClubDbContext db,
        HttpContext http,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!http.User.IsStudentAffairsAdministrator())
            return Results.Forbid();

        var reviewerUserId = http.User.GetUserId();

        var transferRequest = await db.ClubOwnershipTransfers
            .Include(x => x.Club)
            .FirstOrDefaultAsync(x => x.Id == requestId, ct);

        if (transferRequest is null)
            return Results.NotFound(new { message = "Transfer request not found" });

        if (transferRequest.Status != ClubOwnershipTransferStatuses.Pending)
            return Results.BadRequest(new { message = $"Request is already {transferRequest.Status}" });

        transferRequest.Status = ClubOwnershipTransferStatuses.Rejected;
        transferRequest.AdminNote = request.AdminNote;
        transferRequest.ReviewedByUserId = reviewerUserId;
        transferRequest.ReviewedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Club {ClubId} transfer request rejected by admin {AdminId}",
            transferRequest.ClubId, reviewerUserId);

        return Results.Ok(ClubMappers.ToTransferRequestResponse(transferRequest, transferRequest.Club.Name));
    }
}
