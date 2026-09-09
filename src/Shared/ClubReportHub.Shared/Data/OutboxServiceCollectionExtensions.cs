using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubReportHub.Shared.Data;

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionalOutbox<TDbContext>(
        this IServiceCollection services,
        Action<OutboxOptions>? configure = null)
        where TDbContext : DbContext
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<OutboxOptions>();
        }

        services.AddHostedService<OutboxPublisherBackgroundService<TDbContext>>();
        return services;
    }
}
