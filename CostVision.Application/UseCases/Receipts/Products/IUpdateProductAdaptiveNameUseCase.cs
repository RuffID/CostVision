using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public interface IUpdateProductAdaptiveNameUseCase
    {
        Task<ServiceResult> ExecuteAsync(UpdateProductAdaptiveNameRequest request, CancellationToken ct);
    }
}
