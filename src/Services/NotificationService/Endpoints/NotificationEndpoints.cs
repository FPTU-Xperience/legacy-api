using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Data;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Extensions;
using NotificationService.Models;
using System.Security.Claims;

namespace NotificationService.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization(AuthPolicies.AllActors);

        notifications.MapGet("/", GetNotificationsAsync);
        notifications.MapPut("/{id:int}/read", MarkAsReadAsync);
        notifications.MapPut("/read-all", MarkAllAsReadAsync);
    }

    private static async Task<IResult> GetNotificationsAsync(
        int? recipientUserId,
        string? recipientRole,
        bool? unreadOnly,
        NotificationDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var recipientRoles = await NotificationExtensions.GetRecipientRolesAsync(user, clubAccess, httpContext, cancellationToken);
        var query = NotificationExtensions.ScopeNotificationQuery(db.Notifications.AsQueryable(), user, recipientRoles, recipientUserId, recipientRole);
        if (query is null)
        {
            return Results.Forbid();
        }

        if (unreadOnly == true)
        {
            query = query.Where(x => !x.IsRead);
        }

        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync();
        return Results.Ok(rows.Select(NotificationExtensions.ToResponse));
    }

    private static async Task<IResult> MarkAsReadAsync(
        int id,
        NotificationDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.FindAsync([id], cancellationToken);
        if (notification is null)
        {
            return Results.NotFound();
        }

        var recipientRoles = await NotificationExtensions.GetRecipientRolesAsync(user, clubAccess, httpContext, cancellationToken);
        if (!NotificationExtensions.CanAccessNotification(user, recipientRoles, notification))
        {
            return Results.Forbid();
        }

        notification.IsRead = true;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> MarkAllAsReadAsync(
        int? recipientUserId,
        string? recipientRole,
        NotificationDbContext db,
        ClaimsPrincipal user,
        ClubAccessClient clubAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var recipientRoles = await NotificationExtensions.GetRecipientRolesAsync(user, clubAccess, httpContext, cancellationToken);
        var query = NotificationExtensions.ScopeNotificationQuery(db.Notifications.Where(x => !x.IsRead), user, recipientRoles, recipientUserId, recipientRole);
        if (query is null)
        {
            return Results.Forbid();
        }

        await query.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsRead, true));
        return Results.NoContent();
    }
}
