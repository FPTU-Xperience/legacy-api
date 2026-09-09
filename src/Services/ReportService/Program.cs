using ClubReportHub.Shared.Auth;
using ClubReportHub.Shared.Data;
using ClubReportHub.Shared.Messaging;
using ClubReportHub.Shared.Tracing;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReportService.Attachments;
using ReportService.Clients;
using ReportService.Contracts;
using ReportService.Data;
using ReportService.Endpoints;
using ReportService.Jobs;
using ReportService.Models;
using ReportService.Options;
using ReportService.Services;

using KpiContract = ReportService.Contracts;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is required");

builder.Services.AddDbContext<ReportDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.Configure<ReportAttachmentOptions>(builder.Configuration.GetSection(ReportAttachmentOptions.SectionName));
builder.Services.Configure<DemoDataOptions>(builder.Configuration.GetSection("DemoData"));

// Authentication & Authorization & Tracing
builder.Services.AddClubReportJwt(builder.Configuration);
builder.Services.AddClubReportTracing();

// HTTP Clients
builder.Services.AddClubAccessClient(builder.Configuration);
builder.Services.AddRedisStreamEventBus(builder.Configuration);
builder.Services.AddTransactionalOutbox<ReportDbContext>();
builder.Services.AddHttpClient<FinanceWorkflowClient>(client =>
{
    var baseUrl = builder.Configuration["Services:FinanceService:BaseUrl"] ?? "http://localhost:5107";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddCorrelationIdForwarding().AddStandardResilienceHandler();
builder.Services.AddHttpClient<ActivityPublishingClient>(client =>
{
    var baseUrl = builder.Configuration["Services:ActivityService:BaseUrl"] ?? "http://localhost:5106";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddCorrelationIdForwarding().AddStandardResilienceHandler();
builder.Services.AddHttpClient<ClubDirectoryClient>(client =>
{
    var baseUrl = builder.Configuration["Services:ClubService:BaseUrl"] ?? "http://localhost:5102";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddCorrelationIdForwarding().AddStandardResilienceHandler();

// gRPC client for KpiGrpcService with Correlation ID Interceptor
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<CorrelationIdGrpcInterceptor>();
var kpiGrpcUrl = builder.Configuration.GetValue<string>("Services:KpiGrpcService:BaseUrl") ?? "http://localhost:5110";
builder.Services.AddSingleton(sp =>
{
    var interceptor = sp.GetRequiredService<CorrelationIdGrpcInterceptor>();
    var channel = GrpcChannel.ForAddress(kpiGrpcUrl, new GrpcChannelOptions
    {
        HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true }
    });
    var invoker = channel.Intercept(interceptor);
    return new KpiGrpcService.Protos.Client.KpiService.KpiServiceClient(invoker);
});

// Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = TimeSpan.FromSeconds(10)
    }));
builder.Services.AddHangfireServer();
builder.Services.AddScoped<ReportDeadlineJobs>();

// Other services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000", "http://localhost:5173"];
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCorrelationId();

// Exception handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

app.UseConcurrencyExceptionHandler();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorizationFilter()]
});

// Map health and info endpoints
app.MapHealthChecks("/health");
app.MapGet("/error", () => Results.Problem("An unexpected error occurred.")).AllowAnonymous();
app.MapGet("/", () => Results.Ok(new { service = "Report Service", status = "running" }));

// Map API endpoints
app.MapReportEndpoints();
app.MapKpiEndpoints();
app.MapDeadlineEndpoints();

// Initialize database and run migrations
using (var scope = app.Services.CreateScope())
{
    var reportDb = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("DatabaseStartup");
    await reportDb.ApplyMigrationsWithRetryAsync(logger);
    await ReportSchemaUpgrader.ApplyAsync(reportDb);

    // Demo data seeding — only in Development or Docker, only when explicitly enabled
    var demoEnabled = builder.Configuration.GetValue<bool>("DemoData:Enabled");
    var environment = builder.Environment.EnvironmentName;
    if (demoEnabled && (environment == "Development" || environment == "Docker"))
    {
        var resetReports = builder.Configuration.GetValue<bool>("DemoData:ResetReports");
        var demoOptions = new DemoDataOptions();
        builder.Configuration.GetSection("DemoData").Bind(demoOptions);
        var clubIds = demoOptions.GetClubIds();
        var resetService = new DemoResetService(reportDb);
        await DevelopmentDataSeeder.SeedAsync(reportDb, resetService, clubIds, resetReports, logger, CancellationToken.None);
    }
    else
    {
        logger.LogInformation(
            "[DevSeeder] Skipped — DemoData.Enabled={Enabled}, Environment={Env}",
            demoEnabled, environment);
    }
}

// Ensure Hangfire schema is set up
EnsureHangfireSchema(connectionString);

// Register recurring jobs
RecurringJob.AddOrUpdate<ReportDeadlineJobs>(
    "daily-submission-reminder",
    job => job.PublishDailyReminderAsync(CancellationToken.None),
    "0 8 * * *");
RecurringJob.AddOrUpdate<ReportDeadlineJobs>(
    "monthly-missing-report-check",
    job => job.PublishMissingReportCheckAsync(CancellationToken.None),
    "30 8 1 * *");

app.Run();

static void EnsureHangfireSchema(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return;
    }

    using var connection = new SqlConnection(connectionString);
    connection.Open();
    SqlServerObjectsInstaller.Install(connection);
}
