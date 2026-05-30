namespace CostVision.Infrastructure.Services.BackgroundServices
{
    public class MidnightReceiptRefreshBackgroundScheduler : IReceiptRefreshBackgroundScheduler
    {
        public Task WaitForNextRunAsync(CancellationToken ct)
        {
            DateTime nowLocal = DateTime.Now;
            DateTime nextRunLocal = nowLocal.Date.AddDays(1);
            TimeSpan delay = nextRunLocal - nowLocal;

            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            return Task.Delay(delay, ct);
        }
    }
}
