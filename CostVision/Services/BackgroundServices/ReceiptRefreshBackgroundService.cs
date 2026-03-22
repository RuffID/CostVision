using CostVision.Abstractions.Service.Receipts;

namespace CostVision.Services.BackgroundServices
{
    public class ReceiptRefreshBackgroundService(IServiceProvider serviceProvider, ILogger<ReceiptRefreshBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    DateTime nowLocal = DateTime.Now;
                    DateTime nextRunLocal = nowLocal.Date.AddDays(1); // 00:00 следующего дня
                    TimeSpan delay = nextRunLocal - nowLocal;

                    if (delay < TimeSpan.Zero)
                        delay = TimeSpan.Zero;

                    // Ждать наступления 00:00 по локальному времени сервера
                    await Task.Delay(delay, stoppingToken);

                    if (stoppingToken.IsCancellationRequested)                    
                        break;                    

                    using IServiceScope scope = serviceProvider.CreateScope();
                    IReceiptService receiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();

                    logger.LogInformation("[Class:{ClassName}] The update of receipts from the API has been launched.", nameof(ReceiptRefreshBackgroundService));
                    // Обновлять все чеки без Items через ReceiptService
                    await receiptService.RefreshReceiptsWithoutItemsAsync(stoppingToken);
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
