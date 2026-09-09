using AuthService.Data;
using AuthService.Endpoints;
using AuthService.Extensions;
using AuthService.Models;
using AuthService.Services;
using ClubReportHub.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

// Add all services
builder.Services.AddAuthServices(builder.Configuration);

var app = builder.Build();

// ============================================================================
// Pipeline Configuration
// ============================================================================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ============================================================================
// Health & Info Endpoints
// ============================================================================

app.MapHealthChecks("/health");
app.MapGet("/error", () => Results.Problem("An unexpected error occurred.")).AllowAnonymous();
app.MapGet("/", () => Results.Ok(new { service = "Auth Service", status = "running" }));

// ============================================================================
// Map Endpoints
// ============================================================================

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapRoleEndpoints();

// ============================================================================
// Database Initialization
// ============================================================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    await db.ApplyMigrationsWithRetryAsync(logger);

    await AuthSeeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
        builder.Configuration);
}

app.Run();
