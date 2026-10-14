using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Products;

public class GetProductStorePurchasesUseCase(IUnitOfWork unitOfWork) : IGetProductStorePurchasesUseCase
{
    public async Task<ServiceResult<List<ProductStorePurchaseDto>>> ExecuteAsync(GetProductStorePurchasesRequest request, Guid currentUserId, CancellationToken ct)
    {
        if (request.ProductId == Guid.Empty)
        {
            return ServiceResult<List<ProductStorePurchaseDto>>.Fail(ServiceErrorType.Validation, "Товар не указан.");
        }

        Expression<Func<ReceiptItem, bool>> predicate = item => item.ProductId == request.ProductId &&
            item.Receipt != null && item.Receipt.Store != null;
        predicate = predicate.WithReceiptReadAccess(item => item.Receipt, currentUserId);
        List<ProductStorePurchaseDto> result = await unitOfWork.ReceiptItem.GetProjectedItemsAsync(predicate,
            query => query.GroupBy(item => new
            {
                item.Receipt!.StoreId,
                StoreName = item.Receipt.Store!.AdaptiveName ?? item.Receipt.Store.Name,
                IsWeighted = item.ItemsQuantityMeasure == QuantityMeasureType.Gram ||
                    item.ItemsQuantityMeasure == QuantityMeasureType.Kilogram || item.ItemsQuantityMeasure == QuantityMeasureType.Ton
            }).Select(group => new
            {
                group.Key.StoreId, group.Key.StoreName, group.Key.IsWeighted,
                Quantity = group.AsQueryable().Sum(ReceiptFinancialRules.NormalizedItemQuantity),
                Total = group.Sum(item => item.Sum)
            }).OrderBy(item => item.StoreName).ThenBy(item => item.StoreId).ThenBy(item => item.IsWeighted)
            .Select(item => new ProductStorePurchaseDto
            {
                StoreName = item.StoreName, IsWeighted = item.IsWeighted,
                Quantity = item.Quantity, PricePerUnit = item.Quantity == 0 ? 0 : item.Total / item.Quantity
            }), ct);

        return ServiceResult<List<ProductStorePurchaseDto>>.Ok(result);
    }
}
