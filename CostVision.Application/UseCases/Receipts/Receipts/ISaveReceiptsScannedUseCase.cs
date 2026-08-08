using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface ISaveReceiptsScannedUseCase
    {
        Task<ServiceResult<AddReceiptScanResponse>> ExecuteAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct);
    }
}
