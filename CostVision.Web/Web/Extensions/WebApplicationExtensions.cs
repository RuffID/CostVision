using CostVision.Infrastructure.Services.DataBase;

namespace CostVision.Web.Extensions
{
    public static class WebApplicationExtensions
    {
        public static async Task<bool> InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
        {
            using StartupCancellation cancellation = new(ct);

            return await app.Services.GetRequiredService<DatabaseMaintenanceStartup>().RunAsync(cancellation.Token);
        }
    }
}
