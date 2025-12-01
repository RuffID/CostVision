using CostVision.Models.Requests.Receipts;
using CostVision.Models.Services.Receipts;

namespace CostVision.Interfaces.Service.Receipt
{
    public interface IReceiptService
    {
        Task<ReceiptScanResultSummary> SaveScannedReceiptsAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct);

        Task<ManualReceiptResult> SaveManualReceiptAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct);
    }
}