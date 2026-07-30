using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public class UpdateProductAdaptiveNameUseCase(IUnitOfWork unitOfWork) : IUpdateProductAdaptiveNameUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(UpdateProductAdaptiveNameRequest request, CancellationToken ct)
        {
            if (request.ProductId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Товар не указан.");

            if (!Product.TryNormalizeAdaptiveName(request.AdaptiveName, out string? adaptiveName, out string? error))
                return ServiceResult<bool>.Fail(400, error!);

            Product? product = await unitOfWork.Product.GetItemByIdAsync(request.ProductId, ct: ct);
            if (product == null)
                return ServiceResult<bool>.Fail(404, "Товар не найден.");

            if (adaptiveName != null)
            {
                Product? productWithSameAdaptiveName = await unitOfWork.Product.GetItemByPredicateAsync(
                    item => item.Id != product.Id && item.AdaptiveName == adaptiveName,
                    asNoTracking: true,
                    ct: ct);
                if (productWithSameAdaptiveName != null)
                    return ServiceResult<bool>.Fail(409, "Такое Ваше наименование уже задано другому товару.");
            }

            if (!product.TryUpdateAdaptiveName(adaptiveName, out error))
                return ServiceResult<bool>.Fail(400, error!);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
