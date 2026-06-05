using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public interface IGetStoreReceiptListUseCase
    {
        Task<ServiceResult<StoreReceiptListDto>> ExecuteAsync(GetStoreReceiptListRequest request, Guid currentUserId, CancellationToken ct);
    }
}
