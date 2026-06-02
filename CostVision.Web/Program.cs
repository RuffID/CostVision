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
    dbCheckUp.CheckOrUpdateDB();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Моё API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

app.Run();
