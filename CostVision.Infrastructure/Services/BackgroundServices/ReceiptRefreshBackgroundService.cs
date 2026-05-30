using CostVision.Application.UseCases.Receipts.Receipts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CostVision.Infrastructure.Services.BackgroundServices
{
    public class ReceiptRefreshBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ReceiptRefreshBackgroundService> logger,
        IReceiptRefreshBackgroundScheduler scheduler) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await scheduler.WaitForNextRunAsync(stoppingToken);

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    using IServiceScope scope = serviceProvider.CreateScope();
                    IRefreshReceiptsWithoutItemsUseCase refreshReceiptsWithoutItemsUseCase = scope.ServiceProvider.GetRequiredService<IRefreshReceiptsWithoutItemsUseCase>();

                    logger.LogInformation("[Class:{ClassName}] The update of receipts from the API has been launched.", nameof(ReceiptRefreshBackgroundService));
                    await refreshReceiptsWithoutItemsUseCase.ExecuteAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Class:{ClassName}] An error occurred while refreshing receipts.", nameof(ReceiptRefreshBackgroundService));
                }
            }
        }
    }
}
