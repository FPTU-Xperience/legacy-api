namespace ClubReportHub.Shared.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "ClubReportHub";
    public string Audience { get; init; } = "ClubReportHub.Client";
    public string SigningKey { get; init; } = null!;
    public int ExpirationMinutes { get; init; } = 30;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}
