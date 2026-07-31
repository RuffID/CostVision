using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public class GetProductStorePurchasesUseCase(IUnitOfWork unitOfWork) : IGetProductStorePurchasesUseCase
    {
        public async Task<ServiceResult<List<ProductStorePurchaseDto>>> ExecuteAsync(GetProductStorePurchasesRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.ProductId == Guid.Empty)
                return ServiceResult<List<ProductStorePurchaseDto>>.Fail(400, "Товар не указан.");

            List<ReceiptItem> receiptItems = await unitOfWork.ReceiptItem.GetItemsByPredicateAsync(
                item => item.ProductId == request.ProductId &&
                        item.Receipt != null &&
                        item.Receipt.Store != null &&
                        (item.Receipt.CreatedByUserId == currentUserId ||
                         item.Receipt.Accounts.Any(link =>
                             link.Account != null &&
                             (link.Account.CreatedByUserId == currentUserId ||
                              link.Account.Members.Any(member => member.UserId == currentUserId)))),
                asNoTracking: true,
                include: query => query
                    .Include(item => item.Receipt)
                        .ThenInclude(receipt => receipt!.Store)
                    .Include(item => item.Receipt)
                        .ThenInclude(receipt => receipt!.Accounts)
                            .ThenInclude(link => link.Account)
                                .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<ProductStorePurchaseDto> result = receiptItems
                .GroupBy(item => new
                {
                    item.Receipt!.StoreId,
                    IsWeighted = IsWeighted(item.ItemsQuantityMeasure)
                })
                .Select(group => CreateStorePurchaseDto(group))
                .OrderBy(item => item.StoreName)
                .ThenBy(item => item.IsWeighted)
                .ToList();

            return ServiceResult<List<ProductStorePurchaseDto>>.Ok(result);
        }

        private static ProductStorePurchaseDto CreateStorePurchaseDto(IEnumerable<ReceiptItem> items)
        {
            ReceiptItem firstItem = items.First();
            bool isWeighted = IsWeighted(firstItem.ItemsQuantityMeasure);
            decimal quantity = items.Sum(item => NormalizeQuantity(item));
            decimal totalSum = items.Sum(item => item.Sum);
            Store store = firstItem.Receipt!.Store!;

            return new ProductStorePurchaseDto
            {
                StoreName = store.AdaptiveName ?? store.Name,
                Quantity = quantity,
                PricePerUnit = quantity == 0 ? 0 : totalSum / quantity,
                IsWeighted = isWeighted
            };
        }

        private static decimal NormalizeQuantity(ReceiptItem item)
        {
            return item.ItemsQuantityMeasure switch
            {
                QuantityMeasureType.Gram => item.Quantity / 1000m,
                QuantityMeasureType.Kilogram => item.Quantity,
                QuantityMeasureType.Ton => item.Quantity * 1000m,
                _ => item.Quantity
            };
        }

        private static bool IsWeighted(QuantityMeasureType quantityMeasure)
        {
            return quantityMeasure is QuantityMeasureType.Gram or QuantityMeasureType.Kilogram or QuantityMeasureType.Ton;
        }
    }
}
