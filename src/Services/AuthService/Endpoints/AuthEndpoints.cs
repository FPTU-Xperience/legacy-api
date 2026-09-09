using AuthService.Contracts;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using AuthService.Validators;
using ClubReportHub.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Authentication");

        auth.MapPost("/login", HandleLogin)
            .AllowAnonymous()
            .RequireRateLimiting("loginLimit");

        auth.MapPost("/register", HandleRegister)
            .AllowAnonymous()
            .RequireRateLimiting("registerLimit");

        auth.MapPost("/refresh", HandleRefresh)
            .AllowAnonymous()
            .RequireRateLimiting("refreshLimit");

        auth.MapPost("/logout", HandleLogout)
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> HandleLogin(
        LoginRequest request,
        AuthDbContext db,
        IPasswordHasher<User> passwordHasher,
        RefreshTokenService refreshTokenService)
    {
        // Validate input
        var validation = AuthValidators.ValidateLogin(request);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new { message = string.Join(" ", validation.Errors) });
        }

        // Find user
        var user = await db.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x =>
                x.Username == request.Username ||
                x.Email == request.Username);

        // Check user status
        if (user is null || !user.IsActive || user.IsLocked)
        {
            return Results.Unauthorized();
        }

        // Check valid role configuration
        if (!HasValidActorConfiguration(user))
        {
            return Results.Forbid();
        }

        // Verify password
        var passwordResult = passwordHasher.VerifyHashedPassword(
            user, user.PasswordHash, request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Results.Unauthorized();
        }

        // Create tokens
        var refreshToken = await refreshTokenService.CreateRefreshTokenAsync(user.Id);
        return Results.Ok(refreshTokenService.CreateAuthResponse(user, refreshToken));
    }

    private static async Task<IResult> HandleRegister(
        RegisterRequest request,
        AuthDbContext db,
        IPasswordHasher<User> passwordHasher,
        RefreshTokenService refreshTokenService)
    {
        // Validate input
        var validation = AuthValidators.ValidateRegister(request);
        if (!validation.IsValid)
        {
            return Results.BadRequest(new { errors = validation.Errors.ToDictionary(e => e) });
        }

        // Check duplicate
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await db.Users.AnyAsync(x => x.Username == username || x.Email == email))
        {
            return Results.Conflict(new { message = "Username or email already exists." });
        }

        // Get or create CLUB_MEMBER role
        var memberRole = await db.Roles.FirstOrDefaultAsync(x => x.Name == AuthRoles.ClubMember);
        if (memberRole is null)
        {
            memberRole = new Role { Name = AuthRoles.ClubMember };
            db.Roles.Add(memberRole);
            await db.SaveChangesAsync();
        }

        // Create user
        var user = new User
        {
            Username = username,
            FullName = request.FullName.Trim(),
            Email = email,
            IsActive = true
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.UserRoles.Add(new UserRole { User = user, Role = memberRole });

        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Create tokens
        var refreshToken = await refreshTokenService.CreateRefreshTokenAsync(user.Id);
        return Results.Created(
            $"/api/users/{user.Id}",
            refreshTokenService.CreateAuthResponse(user, refreshToken, [AuthRoles.ClubMember]));
    }

    private static async Task<IResult> HandleRefresh(
        RefreshTokenRequest request,
        RefreshTokenService refreshTokenService)
    {
        var oldToken = await refreshTokenService.GetRefreshTokenAsync(request.RefreshToken);
        if (oldToken is null)
        {
            return Results.Unauthorized();
        }

        // Check token validity
        if (!oldToken.IsActive)
        {
            // Token is expired or revoked - revoke entire family for security
            if (oldToken.IsRevoked && !oldToken.IsExpired)
            {
                await refreshTokenService.RevokeFamilyAsync(oldToken.FamilyId, null);
            }
            return Results.Unauthorized();
        }

        // Check valid role
        if (!HasValidActorConfiguration(oldToken.User))
        {
            return Results.Forbid();
        }

        // Rotate token
        var rotatedToken = await refreshTokenService.RotateRefreshTokenAsync(oldToken, null);
        if (rotatedToken is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(refreshTokenService.CreateAuthResponse(oldToken.User, rotatedToken));
    }

    private static async Task<IResult> HandleLogout(
        RefreshTokenRequest request,
        RefreshTokenService refreshTokenService)
    {
        var token = await refreshTokenService.GetRefreshTokenAsync(request.RefreshToken);
        if (token is not null)
        {
            await refreshTokenService.RevokeFamilyAsync(token.FamilyId, null);
        }
        return Results.NoContent();
    }

    private static bool HasValidActorConfiguration(User user)
    {
        var roles = user.UserRoles
            .Select(x => x.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return roles.Length == 1 && AuthRoles.IsKnown(roles[0]);
    }
}
