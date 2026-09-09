using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ClubReportHub.Shared.Data;

public static class ConcurrencyMiddlewareExtensions
{
    /// <summary>
    /// Catches <see cref="DbUpdateConcurrencyException"/> and returns HTTP 409 Conflict
    /// with a user-friendly message when conflicting concurrent updates occur.
    /// </summary>
    public static IApplicationBuilder UseConcurrencyExceptionHandler(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        message = "Dữ liệu đã bị thay đổi bởi người dùng khác. Vui lòng tải lại trang và thử lại."
                    });
                }
            }
        });
    }
}
