using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public interface ISaveManualReceiptUseCase
    {
        Task<ServiceResult<ManualReceiptResult>> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct);
    }
}
