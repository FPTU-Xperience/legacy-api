using ActivityService.Data;
using ActivityService.Infrastructure;
using ActivityService.Services;
using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Messaging;
using Microsoft.EntityFrameworkCore;

namespace ActivityService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddActivityService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ActivityDbContext>(options =>
        {
            var connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not found.");

            options.UseSqlServer(connectionString);
        });

        services.AddClubReportJwt(configuration);
        services.AddClubAccessClient(configuration);

        services.AddScoped<MemberActivityStatisticsService>();

        services.AddHttpClient<ClubMemberRosterClient>(client =>
        {
            var baseUrl =
                configuration["Services:ClubService:BaseUrl"]
                ?? "http://localhost:5102/";

            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddRedisStreamEventBus(configuration);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                var allowedOrigins = configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>()
                    ?? [
                        "http://localhost:3000",
                        "http://localhost:5173"
                    ];

                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddHealthChecks();

        return services;
    }
}