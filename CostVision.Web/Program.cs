using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Services.DataBase;
using CostVision.Infrastructure.Services.Middleware;
using Serilog;
using CostVision.Web.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "Config"))
    .AddJsonFile("config.json");

Log.Logger = new LoggerConfiguration()
    .Enrich.With(new SimpleClassNameEnricher())
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.ConfigureServices(builder);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1000_000_000; // 1 gb
});

WebApplication app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

using (IServiceScope scope = app.Services.CreateScope())
{
    DataBaseCheckUpService<ApplicationContext> dbCheckUp = scope.ServiceProvider.GetRequiredService<DataBaseCheckUpService<ApplicationContext>>();

    const int DATABASE_STARTUP_TIMEOUT_SECONDS = 60;

    try
    {
        await Task.Run(dbCheckUp.CheckOrUpdateDB)
            .WaitAsync(TimeSpan.FromSeconds(DATABASE_STARTUP_TIMEOUT_SECONDS));
    }
    catch (TimeoutException exception)
    {
        Log.Fatal(exception, "[Startup] Database initialization did not finish within {TimeoutSeconds} seconds.", DATABASE_STARTUP_TIMEOUT_SECONDS);
        Environment.Exit(1);
    }
    catch (Exception exception)
    {
        Log.Fatal(exception, "[Startup] Database initialization failed.");
        Environment.Exit(1);
    }
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

app.Run();
