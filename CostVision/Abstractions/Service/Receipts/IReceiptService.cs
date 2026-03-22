using CostVision.Models.Authorization;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Responses.Results;
using CostVision.Models.Services.Receipts;

namespace CostVision.Abstractions.Service.Receipts
{
    public interface IReceiptService
    {
        Task<ReceiptScanResultSummary> SaveReceiptsScannedAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct);

        Task<ManualReceiptResult> SaveReceiptManualAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct);

        Task<ServiceResult<List<Receipt>>> GetReceiptListAsync(User currentUser, DateTime dateFrom, DateTime dateTo, CancellationToken ct);

        Task<ServiceResult<Receipt>> GetReceiptWithItemsAsync(Guid receiptId, User currentUser, CancellationToken ct);

        Task<ServiceResult<Receipt>> RefreshReceiptFromApiAsync(Guid receiptId, User currentUser, CancellationToken ct);

        Task RefreshReceiptsWithoutItemsAsync(CancellationToken ct);

        Task<ServiceResult<bool>> DeleteReceiptAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }
}