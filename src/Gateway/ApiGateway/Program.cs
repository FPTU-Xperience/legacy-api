using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Tracing;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("yarp.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"yarp.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
builder.Services.AddClubReportJwt(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCorrelationId();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "YARP API Gateway", status = "running" }));

app.MapReverseProxy();

await app.RunAsync();
