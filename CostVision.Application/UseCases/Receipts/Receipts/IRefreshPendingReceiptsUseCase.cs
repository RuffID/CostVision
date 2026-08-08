namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IRefreshPendingReceiptsUseCase
    {
        Task ExecuteAsync(CancellationToken ct);
    }
}
