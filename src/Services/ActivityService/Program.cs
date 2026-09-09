using ActivityService.Endpoints;
using ActivityService.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký DbContext, JWT, HttpClient, Redis,
// Swagger, CORS, HealthCheck và các service.
builder.Services.AddActivityService(builder.Configuration);

var app = builder.Build();

// Cấu hình exception handling, Swagger,
// CORS, Authentication và Authorization.
app.UseActivityServicePipeline();

// Đăng ký các nhóm endpoint.
app.MapSystemEndpoints();
app.MapActivityEndpoints();
app.MapMemberStatisticsEndpoints();
app.MapAttendanceManagementEndpoints();

// Khởi tạo database, nâng cấp schema và seed dữ liệu.
await app.InitializeActivityDatabaseAsync();

app.Run();

// Hỗ trợ integration test bằng WebApplicationFactory<Program>.
public partial class Program;