using CostVision.Infrastructure.Services.DataBase;
using Microsoft.Extensions.Options;

namespace CostVision.Web.Extensions
{
    public static class WebApplicationExtensions
    {
        public static async Task<bool> InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
        {
            using StartupCancellation cancellation = new(ct);

            return await InitializeDatabaseServicesAsync(app.Services, cancellation.Token);
        }

        internal static async Task<bool> InitializeDatabaseServicesAsync(IServiceProvider services, CancellationToken ct)
        {
            try
            {
                services.GetRequiredService<IStartupValidator>().Validate();
            }
            catch (Exception)
            {
                services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup")
                    .LogCritical("Application startup failed: Configuration.");
                return false;
            }

            return await services.GetRequiredService<DatabaseMaintenanceStartup>().RunAsync(ct);
        }
    }
}
