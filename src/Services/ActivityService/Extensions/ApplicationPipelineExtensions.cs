namespace ActivityService.Extensions;

public static class ApplicationPipelineExtensions
{
    public static WebApplication UseActivityServicePipeline(
        this WebApplication app)
    {
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

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}