using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Services.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface ISaveReceiptsScannedUseCase
    {
        Task<ReceiptScanResultSummary> ExecuteAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct);
    }
}
