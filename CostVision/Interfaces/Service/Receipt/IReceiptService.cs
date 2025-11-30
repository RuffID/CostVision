using CostVision.Models.Services.Receipts;

namespace CostVision.Interfaces.Service.Receipt
{
    public interface IReceiptService
    {
        Task<ReceiptScanResultSummary> SaveScannedReceiptsAsync(List<QrScanResult> results, Guid currentUserId, CancellationToken ct);

        Task<ManualReceiptResult> SaveManualReceiptAsync(ManualReceiptInput input, Guid currentUserId, CancellationToken ct);
    }
}
