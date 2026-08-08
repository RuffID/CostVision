using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface ISaveReceiptsScannedUseCase
    {
        Task<ServiceResult<ReceiptScanResultSummary>> ExecuteAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct);
    }
}
