using CostVision.Application.UseCases.Receipts.Receipts.Refresh;

namespace CostVision.Infrastructure.Services.BackgroundServices
{
    public class MidnightReceiptRefreshBackgroundScheduler(TimeProvider timeProvider) : IReceiptRefreshBackgroundScheduler
    {
        public Task WaitForNextRunAsync(CancellationToken ct)
        {
            DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            DateTime nextRunUtc = ReceiptRefreshPolicy.GetNextDailyRunUtc(timeProvider);
            TimeSpan delay = nextRunUtc - nowUtc;

            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            return Task.Delay(delay, ct);
        }
    }
}
