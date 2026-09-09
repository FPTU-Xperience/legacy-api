using ClubReportHub.Shared.Tracing;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;

namespace ReportService.Clients;

public sealed class CorrelationIdGrpcInterceptor(IHttpContextAccessor httpContextAccessor) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var correlationId = httpContextAccessor.HttpContext?.GetCorrelationId();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var headers = context.Options.Headers ?? new Metadata();
            headers.Add(CorrelationIdConstants.HeaderName, correlationId);

            var newOptions = context.Options.WithHeaders(headers);
            context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, newOptions);
        }

        return continuation(request, context);
    }
}
