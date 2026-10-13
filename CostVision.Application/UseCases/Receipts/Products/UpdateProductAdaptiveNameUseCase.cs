using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using System.Linq.Expressions;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public class UpdateProductAdaptiveNameUseCase(IUnitOfWork unitOfWork) : IUpdateProductAdaptiveNameUseCase
    {
        public async Task<ServiceResult> ExecuteAsync(UpdateProductAdaptiveNameRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.ProductId == Guid.Empty)
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, "Товар не указан.");
            }

            if (!Product.TryNormalizeAdaptiveName(request.AdaptiveName, out string? adaptiveName, out string? error))
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);
            }

            Product? product = await unitOfWork.Product.GetItemByIdAsync(request.ProductId, ct: ct);
            if (product == null)
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Товар не найден.");
            }

            Expression<Func<Receipt, bool>> purchasePredicate = receipt => receipt.Items.Any(item => item.ProductId == request.ProductId);
            Receipt? accessiblePurchase = await unitOfWork.Receipt.GetItemByPredicateAsync(
                purchasePredicate.WithReceiptReadAccess(receipt => receipt, currentUserId),
                asNoTracking: true,
                ct: ct);
            if (accessiblePurchase == null)
            {
                return ServiceResult.Fail(ServiceErrorType.Forbidden, "Нет доступа к покупкам этого товара.");
            }

            if (adaptiveName != null)
            {
                Product? productWithSameAdaptiveName = await unitOfWork.Product.GetItemByPredicateAsync(
                    item => item.Id != product.Id && item.AdaptiveName == adaptiveName,
                    asNoTracking: true,
                    ct: ct);
                if (productWithSameAdaptiveName != null)
                {
                    return ServiceResult.Fail(ServiceErrorType.Conflict, "Такое общее наименование уже задано другому товару.");
                }
            }

            if (!product.TryUpdateAdaptiveName(adaptiveName, out error))
            {
                return ServiceResult.Fail(ServiceErrorType.Validation, error!);
            }

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult.Ok();
        }
    }
}
