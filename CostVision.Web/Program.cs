using CostVision.Web.Extensions;
using CostVision.Web.Middleware;
using CostVision.Web.Options;
using Microsoft.Extensions.Options;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

try
{
    builder.Configuration.AddCostVisionConfiguration(builder.Environment.ContentRootPath, args);

    Log.Logger = new LoggerConfiguration()
        .Enrich.With(new SimpleClassNameEnricher())
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();

    builder.Host.UseSerilog();
    builder.Services.ConfigureServices(builder);
}
catch (Exception)
{
    Console.Error.WriteLine("Application startup failed: Configuration.");
    Environment.ExitCode = 1;
    return;
}

if (builder.Environment.IsDevelopment())
{
    builder.Host.UseDefaultServiceProvider(options =>
    {
        // Проверяет Captive Dependency во время выполнения
        options.ValidateScopes = true;

        // Проверяет корректность всех регистраций при старте приложения
        options.ValidateOnBuild = true;
    });
}

WebApplication application;
try
{
    application = builder.Build();
}
catch (Exception)
{
    Log.Fatal("Application startup failed: Composition.");
    Environment.ExitCode = 1;
    await Log.CloseAndFlushAsync();
    return;
}

await using WebApplication app = application;

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (!await app.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping))
{
    Environment.ExitCode = 1;
    return;
}

if (app.Services.GetRequiredService<IOptions<SwaggerSettings>>().Value.Enabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CostVision API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

await app.RunAsync();
