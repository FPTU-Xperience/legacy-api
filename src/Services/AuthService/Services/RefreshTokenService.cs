using System.Security.Cryptography;
using AuthService.Contracts;
using AuthService.Data;
using AuthService.Models;
using ClubReportHub.Shared.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

public sealed class RefreshTokenService
{
    private readonly AuthDbContext _db;
    private readonly JwtTokenFactory _tokenFactory;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenService(
        AuthDbContext db,
        JwtTokenFactory tokenFactory,
        IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _tokenFactory = tokenFactory;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(int userId)
    {
        var token = GenerateSecureToken();
        var familyId = Guid.NewGuid().ToString("N");

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            FamilyId = familyId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays)
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
    {
        return await _db.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Token == token);
    }

    public async Task<RefreshToken?> RotateRefreshTokenAsync(
        RefreshToken oldToken,
        string? clientIp)
    {
        if (!oldToken.IsActive)
        {
            return null;
        }

        // Generate new token
        var newToken = GenerateSecureToken();
        var newFamilyId = oldToken.FamilyId;

        // Revoke old token
        oldToken.RevokedAtUtc = DateTimeOffset.UtcNow;
        oldToken.RevokedByIp = clientIp;
        oldToken.ReplacedByToken = newToken;

        var rotatedToken = new RefreshToken
        {
            UserId = oldToken.UserId,
            Token = newToken,
            FamilyId = newFamilyId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays)
        };

        _db.RefreshTokens.Add(rotatedToken);
        await _db.SaveChangesAsync();

        return rotatedToken;
    }

    public async Task RevokeFamilyAsync(string familyId, string? clientIp)
    {
        var tokens = await _db.RefreshTokens
            .Where(x => x.FamilyId == familyId && x.RevokedAtUtc == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            token.RevokedByIp = clientIp;
        }

        await _db.SaveChangesAsync();
    }

    public async Task RevokeForUserAsync(int userId, string? clientIp = null)
    {
        var tokens = await _db.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAtUtc = DateTimeOffset.UtcNow;
            token.RevokedByIp = clientIp;
        }

        await _db.SaveChangesAsync();
    }

    public async Task CleanupExpiredTokensAsync(int daysOld = 30)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-daysOld);
        var expiredTokens = await _db.RefreshTokens
            .Where(x => x.ExpiresAtUtc < cutoff && x.RevokedAtUtc != null)
            .ToListAsync();

        if (expiredTokens.Count > 0)
        {
            _db.RefreshTokens.RemoveRange(expiredTokens);
            await _db.SaveChangesAsync();
        }
    }

    public AuthResponse CreateAuthResponse(
        User user,
        RefreshToken refreshToken,
        IEnumerable<string>? withRoles = null)
    {
        var roles = withRoles?.ToArray() ?? user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray();
        var token = _tokenFactory.CreateToken(user.Id, user.Username, user.FullName, roles);

        return new AuthResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            new UserSummary(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                roles,
                user.IsActive,
                user.IsLocked),
            refreshToken.Token,
            refreshToken.ExpiresAtUtc);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
