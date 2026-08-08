using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Services.DataBase;
using Serilog;

namespace CostVision.Web.Extensions
{
    public static class WebApplicationExtensions
    {
        private const int DATABASE_STARTUP_TIMEOUT_SECONDS = 60;

        public static async Task InitializeDatabaseAsync(this WebApplication app)
        {
            using IServiceScope scope = app.Services.CreateScope();
            DataBaseCheckUpService<ApplicationContext> dbCheckUp = scope.ServiceProvider.GetRequiredService<DataBaseCheckUpService<ApplicationContext>>();

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
    }
}
