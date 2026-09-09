using Microsoft.Extensions.DependencyInjection;

namespace ClubReportHub.Shared.Tracing;

public static class TracingServiceCollectionExtensions
{
    public static IServiceCollection AddClubReportTracing(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<CorrelationIdDelegatingHandler>();
        return services;
    }

    public static IHttpClientBuilder AddCorrelationIdForwarding(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
    }
}
