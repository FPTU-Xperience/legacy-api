using AuthService.Contracts;
using AuthService.Data;
using ClubReportHub.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Endpoints;

public static class RoleEndpoints
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var roles = app.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAuthorization(AuthPolicies.SystemAdministration);

        roles.MapGet("/", HandleGetRoles);
        roles.MapPost("/", HandleCreateRole);

        return app;
    }

    private static async Task<IResult> HandleGetRoles(AuthDbContext db)
    {
        var roles = await db.Roles
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Results.Ok(roles);
    }

    private static async Task<IResult> HandleCreateRole(
        CreateRoleRequest request,
        AuthDbContext db)
    {
        var roleName = request.Name.Trim().ToUpperInvariant();

        // Check if role is predefined
        if (!AuthRoles.IsKnown(roleName))
        {
            return Results.BadRequest(new { message = "Only predefined ClubReportHub actor roles are supported." });
        }

        // Check if already exists
        if (await db.Roles.AnyAsync(x => x.Name == roleName))
        {
            return Results.Conflict(new { message = "Role already exists." });
        }

        var role = new Models.Role { Name = roleName };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return Results.Created($"/api/roles/{role.Id}", role);
    }
}
