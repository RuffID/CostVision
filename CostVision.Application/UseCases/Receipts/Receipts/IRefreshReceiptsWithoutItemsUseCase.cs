namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface IRefreshReceiptsWithoutItemsUseCase
    {
        Task ExecuteAsync(CancellationToken ct);
    }
}
