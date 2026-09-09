using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ClubReportHub.Shared.Auth;

public static class JwtServiceCollectionExtensions
{
    public static IServiceCollection AddClubReportJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("JWT SigningKey must be configured in Jwt:SigningKey section.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Require HTTPS in production, allow HTTP in development
                options.RequireHttpsMetadata = !string.Equals(
                    configuration.GetValue<string>("Environment"),
                    "Development",
                    StringComparison.OrdinalIgnoreCase);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.AllActors, policy => policy.RequireRole(
                AuthRoles.Admin,
                AuthRoles.SystemAdmin,
                AuthRoles.StudentAffairsAdmin,
                AuthRoles.ClubManager,
                AuthRoles.Treasurer,
                AuthRoles.ClubMember));
            options.AddPolicy(AuthPolicies.BusinessAccess, policy => policy.RequireRole(
                AuthRoles.Admin,
                AuthRoles.StudentAffairsAdmin,
                AuthRoles.ClubManager,
                AuthRoles.Treasurer,
                AuthRoles.ClubMember));
            options.AddPolicy(AuthPolicies.SystemAdministration, policy => policy.RequireRole(
                AuthRoles.Admin,
                AuthRoles.SystemAdmin));
            options.AddPolicy(AuthPolicies.StudentAffairsAdministration, policy => policy.RequireRole(
                AuthRoles.Admin,
                AuthRoles.StudentAffairsAdmin));
            options.AddPolicy(AuthPolicies.SuperAdminOnly, policy => policy.RequireRole(AuthRoles.Admin));
            options.AddPolicy(AuthPolicies.AdminOnly, policy => policy.RequireRole(AuthRoles.Admin));
            options.AddPolicy(AuthPolicies.ClubManagerOnly, policy => policy.RequireRole(AuthRoles.ClubManager));
            options.AddPolicy(AuthPolicies.TreasurerOnly, policy => policy.RequireRole(AuthRoles.Treasurer));
            options.AddPolicy(AuthPolicies.ClubMemberOnly, policy => policy.RequireRole(AuthRoles.ClubMember));
            options.AddPolicy(AuthPolicies.AdminOrClubManager, policy => policy.RequireRole(
                AuthRoles.Admin,
                AuthRoles.ClubManager));
            options.AddPolicy(AuthPolicies.AdminOrClubManagerOrMember, policy => policy.RequireRole(
                AuthRoles.Admin,
                AuthRoles.ClubManager,
                AuthRoles.Treasurer,
                AuthRoles.ClubMember));
            options.AddPolicy(AuthPolicies.ClubManagerOrTreasurer, policy => policy.RequireRole(
                AuthRoles.ClubManager,
                AuthRoles.Treasurer));
        });

        services.AddSingleton<JwtTokenFactory>();
        return services;
    }
}
