using Microsoft.AspNetCore.Http;

namespace ClubReportHub.Shared.Tracing;

public sealed class CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = httpContextAccessor.HttpContext?.Items[CorrelationIdConstants.ItemKey]?.ToString()
            ?? httpContextAccessor.HttpContext?.Request.Headers[CorrelationIdConstants.HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        if (!request.Headers.Contains(CorrelationIdConstants.HeaderName))
        {
            request.Headers.Add(CorrelationIdConstants.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
