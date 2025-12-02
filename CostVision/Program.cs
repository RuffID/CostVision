using CostVision.DataBase;
using CostVision.Extensions;
using CostVision.Services.DataBase;
using CostVision.Services.Middleware;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "Config"))
    .AddJsonFile("appsettings.json");

Log.Logger = new LoggerConfiguration()
    .Enrich.With(new SimpleClassNameEnricher())
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.ConfigureServices(builder.Configuration);

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
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Ìî¸ API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

app.Run();
