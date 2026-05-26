using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public class UpdateProductAdaptiveNameUseCase(IUnitOfWork unitOfWork) : IUpdateProductAdaptiveNameUseCase
    {
        private const int MAX_ADAPTIVE_NAME_LENGTH = 500;

        public async Task<ServiceResult<bool>> ExecuteAsync(UpdateProductAdaptiveNameRequest request, CancellationToken ct)
        {
            if (request.ProductId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Товар не указан.");

            if (request.AdaptiveName?.Length > MAX_ADAPTIVE_NAME_LENGTH)
                return ServiceResult<bool>.Fail(400, "Адаптивное название не должно быть длиннее 500 символов.");

            Product? product = await unitOfWork.Product.GetItemByIdAsync(request.ProductId, ct: ct);
            if (product == null)
                return ServiceResult<bool>.Fail(404, "Товар не найден.");

            product.UpdateAdaptiveName(request.AdaptiveName);
            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
