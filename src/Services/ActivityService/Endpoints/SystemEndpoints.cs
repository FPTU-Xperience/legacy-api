namespace ActivityService.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");

        app.MapGet("/error", () =>
                Results.Problem(
                    "An unexpected error occurred."))
            .AllowAnonymous();

        app.MapGet("/", () =>
            Results.Ok(new
            {
                service = "Activity Service",
                status = "running"
            }));

        return app;
    }
}