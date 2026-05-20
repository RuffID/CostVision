using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts.Refresh
{
    public interface IReceiptRefreshWorkflow
    {
        Task<ServiceResult<Receipt>> RefreshAsync(Receipt receipt, CancellationToken ct);

        Task<Receipt> TryRefreshCreatedReceiptAsync(Receipt receipt, CancellationToken ct);

        Task TryRefreshCreatedReceiptsAsync(IEnumerable<Receipt> receipts, CancellationToken ct);
    }
}
