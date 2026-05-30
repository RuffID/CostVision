namespace CostVision.Infrastructure.Services.BackgroundServices
{
    public interface IReceiptRefreshBackgroundScheduler
    {
        Task WaitForNextRunAsync(CancellationToken ct);
    }
}
