using Microsoft.AspNetCore.Http;

namespace ClubReportHub.Shared.Tracing;

public static class CorrelationIdConstants
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public static string? GetCorrelationId(this HttpContext? httpContext)
    {
        return httpContext?.Items[ItemKey]?.ToString()
            ?? httpContext?.Request.Headers[HeaderName].FirstOrDefault();
    }
}
