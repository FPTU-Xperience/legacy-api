using System.Security.Claims;
using AuthService.Contracts;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using AuthService.Validators;
using ClubReportHub.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(AuthPolicies.SystemAdministration);

        users.MapGet("/", HandleGetUsers);
        users.MapPost("/", HandleCreateUser);
        users.MapPut("/{id:int}", HandleUpdateUser);
        users.MapPatch("/{id:int}/lock", HandleLockUser);
        users.MapPatch("/{id:int}/unlock", HandleUnlockUser);

        return app;
    }

    private static async Task<IResult> HandleGetUsers(
        string? search,
        int? page,
        int? pageSize,
        AuthDbContext db,
        CancellationToken cancellationToken)
    {
        var resolvedPage = Math.Max(page ?? 1, 1);
        var resolvedPageSize = pageSize is null or <= 0 or > 100 ? 20 : pageSize.Value;

        var query = db.Users.AsNoTracking().AsQueryable();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Username.Contains(term) ||
                x.FullName.Contains(term) ||
                x.Email.Contains(term) ||
                x.UserRoles.Any(userRole => userRole.Role.Name.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var usersResult = await query
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize)
            .Take(resolvedPageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            items = usersResult.Select(u => UserEndpoints.ToSummary(u)),
            total,
            page = resolvedPage,
            pageSize = resolvedPageSize
        });
    }

    private static async Task<IResult> HandleCreateUser(
        CreateUserRequest request,
        AuthDbContext db,
        IPasswordHasher<User> passwordHasher,
        ClaimsPrincipal actor)
    {
        // Validate input
        var validation = AuthValidators.ValidateCreateUser(request);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new { errors = validation.Errors.ToDictionary(e => e) });
        }

        // Check duplicate
        if (await db.Users.AnyAsync(x =>
            x.Username == request.Username || x.Email == request.Email))
        {
            return Results.Conflict(new { message = "Username or email already exists." });
        }

        // Validate role
        var requestedRoleNames = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (requestedRoleNames.Length != 1 || !AuthRoles.IsKnown(requestedRoleNames[0]))
        {
            return Results.BadRequest(new { message = "Each account must have exactly one predefined actor role." });
        }

        // Only ADMIN can create another ADMIN
        if (requestedRoleNames[0] == AuthRoles.Admin && !actor.IsInRole(AuthRoles.Admin))
        {
            return Results.Forbid();
        }

        // Get role from DB
        var roles = await db.Roles
            .Where(x => requestedRoleNames.Contains(x.Name))
            .ToListAsync();

        if (roles.Count != 1)
        {
            return Results.BadRequest(new { message = "The requested actor role is not available." });
        }

        // Create user
        var user = new User
        {
            Username = request.Username,
            FullName = request.FullName,
            Email = request.Email,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserRoles.AddRange(roles.Select(role =>
            new UserRole { UserId = user.Id, RoleId = role.Id }));
        await db.SaveChangesAsync();

        return Results.Created($"/api/users/{user.Id}", ToSummary(user, roles.Select(x => x.Name)));
    }

    private static async Task<IResult> HandleUpdateUser(
        int id,
        UpdateUserRequest request,
        AuthDbContext db,
        ClaimsPrincipal actor,
        RefreshTokenService refreshTokenService)
    {
        // Validate input
        var validation = AuthValidators.ValidateUpdateUser(request);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new { errors = validation.Errors.ToDictionary(e => e) });
        }

        // Find user
        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
        {
            return Results.NotFound();
        }

        // Check email duplicate
        var trimmedEmail = request.Email.Trim();
        if (await db.Users.AnyAsync(x => x.Id != id && x.Email == trimmedEmail))
        {
            return Results.Conflict(new { message = "Email already belongs to another account." });
        }

        // Validate role
        var requestedRoleNames = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (requestedRoleNames.Length != 1 || !AuthRoles.IsKnown(requestedRoleNames[0]))
        {
            return Results.BadRequest(new { message = "Each account must have exactly one predefined actor role." });
        }

        // Get role from DB
        var roles = await db.Roles
            .Where(x => requestedRoleNames.Contains(x.Name))
            .ToListAsync();

        if (roles.Count != requestedRoleNames.Length)
        {
            return Results.BadRequest(new { message = "One or more requested roles are invalid." });
        }

        var requestedRoleName = roles.Single().Name;
        var currentRoleName = user.UserRoles.SingleOrDefault()?.Role.Name;
        var targetIsSuperAdmin = currentRoleName == AuthRoles.Admin;

        // Check authorization for ADMIN operations
        if (!actor.IsInRole(AuthRoles.Admin) &&
            (targetIsSuperAdmin || requestedRoleName == AuthRoles.Admin))
        {
            return Results.Forbid();
        }

        // Prevent self-deactivation or self-role-change
        if (id == actor.GetUserId() &&
            (!request.IsActive ||
             !string.Equals(currentRoleName, requestedRoleName, StringComparison.OrdinalIgnoreCase)))
        {
            return Results.BadRequest(new { message = "You cannot deactivate your own account or change your own actor role." });
        }

        // Prevent deactivating final ADMIN
        if (targetIsSuperAdmin &&
            (!request.IsActive || requestedRoleName != AuthRoles.Admin))
        {
            var otherActiveSuperAdmins = await db.Users.CountAsync(x =>
                x.Id != id &&
                x.IsActive &&
                !x.IsLocked &&
                x.UserRoles.Any(ur => ur.Role.Name == AuthRoles.Admin));

            if (otherActiveSuperAdmins == 0)
            {
                return Results.Conflict(new { message = "The final active ADMIN account cannot be deactivated or reassigned." });
            }
        }

        // Update user
        user.FullName = request.FullName.Trim();
        user.Email = trimmedEmail;
        user.IsActive = request.IsActive;

        db.UserRoles.RemoveRange(user.UserRoles);
        db.UserRoles.AddRange(roles.Select(role =>
            new UserRole { UserId = user.Id, RoleId = role.Id }));

        await db.SaveChangesAsync();

        // Revoke tokens if status changed
        if (!request.IsActive ||
            !string.Equals(currentRoleName, requestedRoleName, StringComparison.OrdinalIgnoreCase))
        {
            await refreshTokenService.RevokeForUserAsync(user.Id);
        }

        return Results.Ok(ToSummary(user, roles.Select(x => x.Name)));
    }

    private static async Task<IResult> HandleLockUser(
        int id,
        AuthDbContext db,
        ClaimsPrincipal actor,
        RefreshTokenService refreshTokenService)
    {
        // Find user
        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
        {
            return Results.NotFound();
        }

        // Prevent self-lock
        if (id == actor.GetUserId())
        {
            return Results.BadRequest(new { message = "You cannot lock your own account." });
        }

        var targetIsSuperAdmin = user.UserRoles.Any(x => x.Role.Name == AuthRoles.Admin);
        if (targetIsSuperAdmin && !actor.IsInRole(AuthRoles.Admin))
        {
            return Results.Forbid();
        }

        // Prevent locking final ADMIN
        if (targetIsSuperAdmin && user.IsActive && !user.IsLocked)
        {
            var otherActiveSuperAdmins = await db.Users.CountAsync(x =>
                x.Id != id &&
                x.IsActive &&
                !x.IsLocked &&
                x.UserRoles.Any(ur => ur.Role.Name == AuthRoles.Admin));

            if (otherActiveSuperAdmins == 0)
            {
                return Results.Conflict(new { message = "The final active ADMIN account cannot be locked." });
            }
        }

        user.IsLocked = true;
        await db.SaveChangesAsync();
        await refreshTokenService.RevokeForUserAsync(user.Id);

        return Results.NoContent();
    }

    private static async Task<IResult> HandleUnlockUser(
        int id,
        AuthDbContext db,
        ClaimsPrincipal actor)
    {
        // Find user
        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (user is null)
        {
            return Results.NotFound();
        }

        // Prevent self-unlock
        if (id == actor.GetUserId())
        {
            return Results.BadRequest(new { message = "You cannot unlock your own account." });
        }

        // Check ADMIN authorization
        if (user.UserRoles.Any(x => x.Role.Name == AuthRoles.Admin) &&
            !actor.IsInRole(AuthRoles.Admin))
        {
            return Results.Forbid();
        }

        user.IsLocked = false;
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    public static UserSummary ToSummary(User user, IEnumerable<string>? withRoles = null)
    {
        var roles = withRoles?.ToArray() ??
            user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray();

        return new UserSummary(
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            roles,
            user.IsActive,
            user.IsLocked);
    }
}
