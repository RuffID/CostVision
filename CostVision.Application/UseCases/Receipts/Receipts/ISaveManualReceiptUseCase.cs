using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface ISaveManualReceiptUseCase
    {
        Task<ServiceResult<AddReceiptManualResponse>> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct);
    }
}
