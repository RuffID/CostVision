using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.Receipts.Products
{
    public class GetProductListUseCase(IUnitOfWork unitOfWork) : IGetProductListUseCase
    {
        private const int MIN_PAGE = 1;
        private const int MIN_PAGE_SIZE = 1;
        private const int MAX_PAGE_SIZE = 100;
        private const string SORT_BY_NAME = "name";
        private const string SORT_BY_RECEIPT_COUNT = "receiptCount";
        private const string SORT_BY_AVERAGE_PRICE = "averagePrice";
        private const string SORT_DIRECTION_DESC = "desc";

        public async Task<ServiceResult<ProductListDto>> ExecuteAsync(GetProductListRequest request, Guid currentUserId, CancellationToken ct)
        {
            int page = Math.Max(request.Page, MIN_PAGE);
            int pageSize = Math.Clamp(request.PageSize, MIN_PAGE_SIZE, MAX_PAGE_SIZE);
            string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
            Expression<Func<Product, bool>> predicate = product =>
                product.ReceiptItems.Any(item =>
                    item.Receipt != null &&
                    (
                        item.Receipt.CreatedByUserId == currentUserId ||
                        item.Receipt.Accounts.Any(link =>
                            link.Account != null &&
                            (
                                link.Account.CreatedByUserId == currentUserId ||
                                link.Account.Members.Any(member => member.UserId == currentUserId)
                            ))
                    )) &&
                (search == null ||
                 product.Name.Contains(search) ||
                 (product.AdaptiveName != null && product.AdaptiveName.Contains(search)));
            int totalCount = await unitOfWork.Product.CountByPredicateAsync(predicate, ct);
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            int skip = (page - 1) * pageSize;

            List<Product> products = await unitOfWork.Product.GetItemsByPredicateAsync(
                predicate,
                asNoTracking: true,
                include: query => query
                    .Include(product => product.ReceiptItems)
                    .ThenInclude(item => item.Receipt)
                    .ThenInclude(receipt => receipt!.Accounts)
                    .ThenInclude(link => link.Account)
                    .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<ProductListItemDto> allItems = products
                .Select(product => CreateProductListItem(product, request.UseAdaptiveNames, currentUserId))
                .ToList();

            List<ProductListItemDto> items = SortItems(allItems, request.SortBy, request.SortDirection)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            ProductListDto result = new()
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > MIN_PAGE,
                HasNextPage = page < totalPages
            };

            return ServiceResult<ProductListDto>.Ok(result);
        }

        private static List<ProductListItemDto> SortItems(List<ProductListItemDto> items, string? sortBy, string? sortDirection)
        {
            bool isDescending = string.Equals(sortDirection, SORT_DIRECTION_DESC, StringComparison.OrdinalIgnoreCase);
            string normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? SORT_BY_NAME : sortBy.Trim();

            IOrderedEnumerable<ProductListItemDto> orderedItems = normalizedSortBy switch
            {
                SORT_BY_RECEIPT_COUNT => isDescending
                    ? items.OrderByDescending(item => item.ReceiptCount).ThenBy(item => item.Name)
                    : items.OrderBy(item => item.ReceiptCount).ThenBy(item => item.Name),
                SORT_BY_AVERAGE_PRICE => isDescending
                    ? items.OrderByDescending(item => item.AveragePrice.HasValue).ThenByDescending(item => item.AveragePrice).ThenBy(item => item.Name)
                    : items.OrderByDescending(item => item.AveragePrice.HasValue).ThenBy(item => item.AveragePrice).ThenBy(item => item.Name),
                _ => isDescending
                    ? items.OrderByDescending(item => item.Name).ThenByDescending(item => item.ReceiptCount)
                    : items.OrderBy(item => item.Name).ThenByDescending(item => item.ReceiptCount)
            };

            return orderedItems.ToList();
        }

        private static ProductListItemDto CreateProductListItem(Product product, bool useAdaptiveNames, Guid currentUserId)
        {
            List<ReceiptItem> accessibleReceiptItems = product.ReceiptItems
                .Where(item => item.Receipt != null && IsReceiptAccessible(item.Receipt, currentUserId))
                .ToList();
            List<bool> weightedVariants = accessibleReceiptItems
                .Select(item => IsWeighted(item.ItemsQuantityMeasure))
                .Distinct()
                .ToList();
            bool hasSingleQuantityMeasure = weightedVariants.Count == 1;
            decimal totalQuantity = accessibleReceiptItems.Sum(NormalizeQuantity);

            return new ProductListItemDto
            {
                Id = product.Id,
                Name = product.Name,
                AdaptiveName = product.AdaptiveName,
                DisplayName = useAdaptiveNames && !string.IsNullOrWhiteSpace(product.AdaptiveName)
                    ? product.AdaptiveName
                    : product.Name,
                ReceiptCount = accessibleReceiptItems.Select(item => item.ReceiptId).Distinct().Count(),
                AveragePrice = hasSingleQuantityMeasure && totalQuantity != 0
                    ? accessibleReceiptItems.Sum(item => item.Sum) / totalQuantity
                    : null,
                AveragePriceIsWeighted = hasSingleQuantityMeasure ? weightedVariants[0] : null
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

        private static bool IsReceiptAccessible(Receipt receipt, Guid currentUserId)
        {
            if (receipt.CreatedByUserId == currentUserId)
                return true;

            return receipt.Accounts.Any(link =>
                link.Account != null &&
                (
                    link.Account.CreatedByUserId == currentUserId ||
                    link.Account.Members.Any(member => member.UserId == currentUserId)
                ));
        }

    }
}
