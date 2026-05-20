using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Services.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface ISaveManualReceiptUseCase
    {
        Task<ManualReceiptResult> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct);
    }
}
