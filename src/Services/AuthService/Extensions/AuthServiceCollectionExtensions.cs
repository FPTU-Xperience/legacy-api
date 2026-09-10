using System.Security.Claims;
using System.Threading.RateLimiting;
using AuthService.Data;
using AuthService.Models;
using ClubReportHub.Shared.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Extensions;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Password Hasher
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        // JWT Authentication
        services.AddClubReportJwt(configuration);

        // Refresh Token Service
        services.AddScoped<Services.RefreshTokenService>();

        // Rate Limiting
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Strict rate limit for login attempts
            options.AddPolicy("loginLimit", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Moderate limit for registration
            options.AddPolicy("registerLimit", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(5),
                        SegmentsPerWindow = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Refresh token limit
            options.AddPolicy("refreshLimit", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 5,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        // API Documentation
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                var defaultOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "http://localhost:3000",
                    "http://localhost:3001",
                    "http://localhost:5173",
                    "https://fptux-legacy-ui.pages.dev"
                };

                foreach (var origin in configuredOrigins)
                {
                    if (!string.IsNullOrWhiteSpace(origin))
                    {
                        defaultOrigins.Add(origin.Trim().TrimEnd('/'));
                    }
                }

                policy.SetIsOriginAllowed(origin =>
                      {
                          if (string.IsNullOrWhiteSpace(origin)) return false;
                          if (defaultOrigins.Contains(origin.TrimEnd('/'))) return true;

                          if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                          {
                              return uri.Host == "localhost"
                                  || uri.Host == "127.0.0.1"
                                  || uri.Host.EndsWith(".pages.dev", StringComparison.OrdinalIgnoreCase);
                          }

                          return false;
                      })
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        // Health Checks
        services.AddHealthChecks();

        return services;
    }
}
