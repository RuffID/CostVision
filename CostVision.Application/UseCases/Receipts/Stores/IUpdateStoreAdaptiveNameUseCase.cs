using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public interface IUpdateStoreAdaptiveNameUseCase
    {
        Task<ServiceResult> ExecuteAsync(UpdateStoreAdaptiveNameRequest request, Guid currentUserId, CancellationToken ct);
    }
}
