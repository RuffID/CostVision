using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public interface IGetProductStorePurchasesUseCase
    {
        Task<ServiceResult<List<ProductStorePurchaseDto>>> ExecuteAsync(GetProductStorePurchasesRequest request, Guid currentUserId, CancellationToken ct);
    }
}
