using System.ComponentModel.DataAnnotations;

namespace AuthService.Contracts;

public sealed record LoginRequest(
    [StringLength(100)] string Username,
    string Password);

public sealed record RegisterRequest(
    [StringLength(100)] string Username,
    [StringLength(200)] string FullName,
    [StringLength(255), EmailAddress] string Email,
    string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    UserSummary User,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAtUtc = null);

public sealed record UserSummary(
    int Id,
    string Username,
    string FullName,
    string Email,
    IReadOnlyCollection<string> Roles,
    bool IsActive,
    bool IsLocked);

public sealed record CreateUserRequest(
    [StringLength(100)] string Username,
    [StringLength(200)] string FullName,
    [StringLength(255), EmailAddress] string Email,
    [StringLength(100, MinimumLength = 8)] string Password,
    IReadOnlyCollection<string> Roles);

public sealed record UpdateUserRequest(
    [StringLength(200)] string FullName,
    [StringLength(255), EmailAddress] string Email,
    bool IsActive,
    IReadOnlyCollection<string> Roles);

public sealed record CreateRoleRequest([StringLength(50)] string Name);
