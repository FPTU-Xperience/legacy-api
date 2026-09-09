using System.Security.Claims;
using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Events;
using ClubReportHub.Shared.Messaging;
using ClubService.Contracts;
using ClubService.Data;
using ClubService.Extensions;
using ClubService.Mappers;
using ClubService.Models;
using Microsoft.EntityFrameworkCore;

namespace ClubService.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        var clubs = app.MapGroup("/api/clubs")
            .WithTags("Club Applications")
            .RequireAuthorization(AuthPolicies.BusinessAccess);

        // GET /api/clubs/applications - Get all applications (Admin only)
        clubs.MapGet("/applications", GetAllApplications)
            .WithName("GetAllApplications")
            .WithDescription("Get all club creation applications (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        // GET /api/clubs/applications/me - Get my applications
        clubs.MapGet("/applications/me", GetMyApplications)
            .WithName("GetMyApplications")
            .WithDescription("Get current user's club creation applications");

        // POST /api/clubs/applications - Submit new application
        clubs.MapPost("/applications", SubmitApplication)
            .WithName("SubmitApplication")
            .WithDescription("Submit a new club creation application")
            .RequireAuthorization(AuthPolicies.ClubMemberOnly);

        // PUT /api/clubs/applications/{applicationId} - Update application (resubmit)
        clubs.MapPut("/applications/{applicationId:int}", UpdateApplication)
            .WithName("UpdateApplication")
            .WithDescription("Update and resubmit a club creation application")
            .RequireAuthorization(AuthPolicies.ClubMemberOnly);

        // POST /api/clubs/applications/{applicationId}/approve - Approve application (Admin only)
        clubs.MapPost("/applications/{applicationId:int}/approve", ApproveApplication)
            .WithName("ApproveApplication")
            .WithDescription("Approve a club creation application (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        // POST /api/clubs/applications/{applicationId}/request-revision - Request revision (Admin only)
        clubs.MapPost("/applications/{applicationId:int}/request-revision", RequestRevision)
            .WithName("RequestApplicationRevision")
            .WithDescription("Request revision for a club application (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);

        // POST /api/clubs/applications/{applicationId}/reject - Reject application (Admin only)
        clubs.MapPost("/applications/{applicationId:int}/reject", RejectApplication)
            .WithName("RejectApplication")
            .WithDescription("Reject a club creation application (Admin only)")
            .RequireAuthorization(AuthPolicies.StudentAffairsAdministration);
    }

    private static async Task<IResult> GetAllApplications(ClubDbContext db)
    {
        var applications = await db.ClubCreationApplications
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ToListAsync();

        return Results.Ok(applications.Select(ClubMappers.ToApplicationResponse));
    }

    private static async Task<IResult> GetMyApplications(
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        var userId = user.GetUserId();
        var applications = await db.ClubCreationApplications
            .Where(x => x.RequesterUserId == userId)
            .OrderByDescending(x => x.SubmittedAtUtc)
            .ToListAsync();

        return Results.Ok(applications.Select(ClubMappers.ToApplicationResponse));
    }

    private static async Task<IResult> SubmitApplication(
        CreateClubApplicationRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        var userId = user.GetUserId();
        var code = ValidationExtensions.NormalizeOrGenerateClubCode(request.Code, userId);

        var validationError = ValidationExtensions.ValidateClubApplication(request, code);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        if (await db.ClubManagerAssignments.AnyAsync(x => x.ManagerUserId == userId && x.IsActive))
        {
            return Results.Conflict(new { message = "Each club owner can manage one club only." });
        }

        if (await db.Clubs.AnyAsync(x => x.Code == code))
        {
            return Results.Conflict(new { message = "Club code already exists." });
        }

        if (await db.ClubCreationApplications.AnyAsync(x => x.RequesterUserId == userId && x.Status == ClubApplicationStatuses.Submitted))
        {
            return Results.Conflict(new { message = "You already have a pending club creation application." });
        }

        var application = new ClubCreationApplication { RequesterUserId = userId };
        ClubMappers.ApplyClubApplicationRequest(application, request, code);

        db.ClubCreationApplications.Add(application);
        await db.SaveChangesAsync();

        return Results.Created($"/api/clubs/applications/{application.Id}", ClubMappers.ToApplicationResponse(application));
    }

    private static async Task<IResult> UpdateApplication(
        int applicationId,
        CreateClubApplicationRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        var application = await db.ClubCreationApplications.FirstOrDefaultAsync(x => x.Id == applicationId);
        if (application is null)
        {
            return Results.NotFound();
        }

        if (application.RequesterUserId != user.GetUserId())
        {
            return Results.Forbid();
        }

        if (application.Status != ClubApplicationStatuses.NeedsRevision)
        {
            return Results.Conflict(new { message = "Only applications requiring revision can be updated and resubmitted." });
        }

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? application.Code
            : ValidationExtensions.NormalizeOrGenerateClubCode(request.Code, application.RequesterUserId);

        var validationError = ValidationExtensions.ValidateClubApplication(request, code);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        if (await db.Clubs.AnyAsync(x => x.Code == code))
        {
            return Results.Conflict(new { message = "The club code already exists." });
        }

        ClubMappers.ApplyClubApplicationRequest(application, request, code);
        application.Status = ClubApplicationStatuses.Submitted;
        application.ReviewNote = null;
        application.ReviewConditions = null;
        application.ReviewerSignature = null;
        application.ReviewedAtUtc = null;
        application.ReviewedByUserId = null;
        application.SubmittedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(ClubMappers.ToApplicationResponse(application));
    }

    private static async Task<IResult> ApproveApplication(
        int applicationId,
        ReviewClubApplicationRequest request,
        ClaimsPrincipal user,
        ClubDbContext db,
        IEventBus eventBus,
        CancellationToken cancellationToken)
    {
        var validationError = ValidationExtensions.ValidateReviewContent(
            request.Note, request.Conditions, request.ReviewerSignature);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var application = await db.ClubCreationApplications
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return Results.NotFound();
        }

        if (application.Status != ClubApplicationStatuses.Submitted)
        {
            return Results.Conflict(new { message = "Application was already reviewed." });
        }

        if (await db.ClubManagerAssignments.AnyAsync(
            x => x.ManagerUserId == application.RequesterUserId && x.IsActive, cancellationToken))
        {
            return Results.Conflict(new { message = "Requester already owns a club." });
        }

        if (await db.Clubs.AnyAsync(x => x.Code == application.Code, cancellationToken))
        {
            return Results.Conflict(new { message = "Club code already exists." });
        }

        var club = new Club
        {
            Code = application.Code,
            Name = application.Name,
            Category = application.Category,
            Description = application.Description,
            LogoUrl = application.LogoUrl,
            ContactEmail = application.ContactEmail,
            ContactPhone = application.ContactPhone
        };

        club.ManagerAssignments.Add(new ClubManagerAssignment
        {
            ManagerUserId = application.RequesterUserId,
            ManagerName = application.RequesterName,
            IsActive = true
        });

        club.Memberships.Add(new ClubMembership
        {
            UserId = application.RequesterUserId,
            FullName = application.RequesterName,
            Email = application.ContactEmail,
            PhoneNumber = application.ContactPhone,
            Address = application.FounderOrganization,
            Role = ClubMemberRoles.Member,
            Status = ClubMembershipStatuses.Approved,
            AcceptedClubRules = true,
            CommittedToParticipate = true,
            ReviewedAtUtc = DateTimeOffset.UtcNow,
            ReviewedByUserId = user.GetUserId()
        });

        db.Clubs.Add(club);
        await db.SaveChangesAsync(cancellationToken);

        application.Status = ClubApplicationStatuses.Approved;
        application.ReviewNote = request.Note?.Trim();
        application.ReviewConditions = request.Conditions?.Trim();
        application.ReviewerSignature = request.ReviewerSignature?.Trim();
        application.CreatedClubId = club.Id;
        application.ReviewedAtUtc = DateTimeOffset.UtcNow;
        application.ReviewedByUserId = user.GetUserId();
        await db.SaveChangesAsync(cancellationToken);

        await eventBus.PublishAsync(new ClubCreatedEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            club.Id,
            club.Code,
            club.Name), EventRoutingKeys.ClubCreated, cancellationToken);

        return Results.Ok(ClubMappers.ToApplicationResponse(application));
    }

    private static async Task<IResult> RequestRevision(
        int applicationId,
        ReviewClubApplicationRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return Results.BadRequest(new { message = "Please specify the required changes." });
        }

        var validationError = ValidationExtensions.ValidateReviewContent(
            request.Note, request.Conditions, request.ReviewerSignature);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var application = await db.ClubCreationApplications.FirstOrDefaultAsync(x => x.Id == applicationId);
        if (application is null)
        {
            return Results.NotFound();
        }

        if (application.Status != ClubApplicationStatuses.Submitted)
        {
            return Results.Conflict(new { message = "This application has already been processed." });
        }

        application.Status = ClubApplicationStatuses.NeedsRevision;
        application.ReviewNote = request.Note.Trim();
        application.ReviewConditions = request.Conditions?.Trim();
        application.ReviewerSignature = request.ReviewerSignature?.Trim();
        application.ReviewedAtUtc = DateTimeOffset.UtcNow;
        application.ReviewedByUserId = user.GetUserId();

        await db.SaveChangesAsync();
        return Results.Ok(ClubMappers.ToApplicationResponse(application));
    }

    private static async Task<IResult> RejectApplication(
        int applicationId,
        ReviewClubApplicationRequest request,
        ClaimsPrincipal user,
        ClubDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
        {
            return Results.BadRequest(new { message = "Please provide a rejection reason." });
        }

        var validationError = ValidationExtensions.ValidateReviewContent(
            request.Note, request.Conditions, request.ReviewerSignature);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var application = await db.ClubCreationApplications.FirstOrDefaultAsync(x => x.Id == applicationId);
        if (application is null)
        {
            return Results.NotFound();
        }

        if (application.Status != ClubApplicationStatuses.Submitted)
        {
            return Results.Conflict(new { message = "Application was already reviewed." });
        }

        application.Status = ClubApplicationStatuses.Rejected;
        application.ReviewNote = request.Note?.Trim();
        application.ReviewConditions = request.Conditions?.Trim();
        application.ReviewerSignature = request.ReviewerSignature?.Trim();
        application.ReviewedAtUtc = DateTimeOffset.UtcNow;
        application.ReviewedByUserId = user.GetUserId();

        await db.SaveChangesAsync();
        return Results.Ok(ClubMappers.ToApplicationResponse(application));
    }
}
