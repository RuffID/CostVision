using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public interface IGetStoreListUseCase
    {
        Task<ServiceResult<StoreListDto>> ExecuteAsync(GetStoreListRequest request, Guid currentUserId, CancellationToken ct);
    }
}
