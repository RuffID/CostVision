using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public interface IGetProductListUseCase
    {
        Task<ServiceResult<ProductListDto>> ExecuteAsync(GetProductListRequest request, CancellationToken ct);
    }
}
