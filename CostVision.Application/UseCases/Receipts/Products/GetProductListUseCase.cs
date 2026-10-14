using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Products;

public class GetProductListUseCase(IUnitOfWork unitOfWork) : IGetProductListUseCase
{
    public async Task<ServiceResult<ProductListDto>> ExecuteAsync(GetProductListRequest request, Guid currentUserId, CancellationToken ct)
    {
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        string? search = request.Search?.Trim();
        DateTime? periodStart = request.DateFrom?.Date;
        DateTime? periodEnd = request.DateTo?.Date.AddDays(1);
        Expression<Func<ReceiptItem, bool>> itemPredicate = item =>
            item.Receipt != null &&
            (!periodStart.HasValue || item.Receipt.DateTime >= periodStart.Value) &&
            (!periodEnd.HasValue || item.Receipt.DateTime < periodEnd.Value) &&
            (!request.AccountId.HasValue || item.Receipt.Accounts.Any(link =>
                link.AccountId == request.AccountId.Value && link.Account != null &&
                (link.Account.CreatedByUserId == currentUserId ||
                 link.Account.Members.Any(member => member.UserId == currentUserId))));
        itemPredicate = itemPredicate.WithReceiptReadAccess(item => item.Receipt, currentUserId);
        Expression<Func<Product, bool>> predicate = product =>
            product.ReceiptItems.AsQueryable().Any(itemPredicate) &&
            (search == null || search == "" || product.Name.Contains(search) ||
             (product.AdaptiveName != null && product.AdaptiveName.Contains(search)));

        List<ReadQueryTotals> summaries = await unitOfWork.Product.GetProjectedItemsAsync(predicate,
            query => Project(query, itemPredicate, request.UseAdaptiveNames).GroupBy(item => 1)
                .Select(group => new ReadQueryTotals
                {
                    Count = group.Count(),
                    Expense = group.Sum(item => item.TotalSum)
                }), ct);
        ReadQueryTotals totals = summaries.SingleOrDefault() ?? new ReadQueryTotals();
        int totalPages = Math.Max(1, (int)Math.Ceiling(totals.Count / (double)pageSize));
        int page = Math.Clamp(request.Page, 1, totalPages);
        List<ProductListItemDto> items = await unitOfWork.Product.GetProjectedItemsAsync(predicate,
            query => Sort(Project(query, itemPredicate, request.UseAdaptiveNames), request.SortBy, request.SortDirection)
                .Skip((page - 1) * pageSize).Take(pageSize), ct);

        return ServiceResult<ProductListDto>.Ok(new ProductListDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = totals.Count,
            TotalPages = totalPages, HasPreviousPage = page > 1, HasNextPage = page < totalPages,
            TotalSum = totals.Expense
        });
    }

    internal static IQueryable<ProductListItemDto> Project(IQueryable<Product> source,
        Expression<Func<ReceiptItem, bool>> predicate, bool useAdaptiveNames)
    {
        return source.Select(product => new
        {
            product.Id, product.Name, product.AdaptiveName,
            ReceiptCount = product.ReceiptItems.AsQueryable().Where(predicate).Select(item => item.ReceiptId).Distinct().Count(),
            TotalSum = product.ReceiptItems.AsQueryable().Where(predicate).Sum(item => item.Sum),
            Quantity = product.ReceiptItems.AsQueryable().Where(predicate).Sum(ReceiptFinancialRules.NormalizedItemQuantity),
            WeightedKinds = product.ReceiptItems.AsQueryable().Where(predicate).Select(ReceiptFinancialRules.WeightedItem).Distinct().Count(),
            IsWeighted = product.ReceiptItems.AsQueryable().Where(predicate).Any(ReceiptFinancialRules.WeightedItem)
        }).Select(product => new ProductListItemDto
        {
            Id = product.Id, Name = product.Name, AdaptiveName = product.AdaptiveName,
            DisplayName = useAdaptiveNames && product.AdaptiveName != null && product.AdaptiveName != "" ? product.AdaptiveName : product.Name,
            ReceiptCount = product.ReceiptCount, TotalSum = product.TotalSum,
            AveragePrice = product.WeightedKinds == 1 && product.Quantity != 0 ? product.TotalSum / product.Quantity : null,
            AveragePriceIsWeighted = product.WeightedKinds == 1 ? product.IsWeighted : null
        });
    }

    private static IOrderedQueryable<ProductListItemDto> Sort(IQueryable<ProductListItemDto> items, string? sortBy, string? sortDirection)
    {
        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<ProductListItemDto> ordered;
        if (sortBy == "receiptCount")
        {
            if (descending)
            {
                ordered = items.OrderByDescending(item => item.ReceiptCount).ThenBy(item => item.Name);
            }
            else
            {
                ordered = items.OrderBy(item => item.ReceiptCount).ThenBy(item => item.Name);
            }
        }
        else if (sortBy == "averagePrice")
        {
            if (descending)
            {
                ordered = items.OrderByDescending(item => item.AveragePrice.HasValue).ThenByDescending(item => item.AveragePrice).ThenBy(item => item.Name);
            }
            else
            {
                ordered = items.OrderByDescending(item => item.AveragePrice.HasValue).ThenBy(item => item.AveragePrice).ThenBy(item => item.Name);
            }
        }
        else if (descending)
        {
            ordered = items.OrderByDescending(item => item.Name).ThenByDescending(item => item.ReceiptCount);
        }
        else
        {
            ordered = items.OrderBy(item => item.Name).ThenByDescending(item => item.ReceiptCount);
        }

        return ordered.ThenBy(item => item.Id);
    }
}
