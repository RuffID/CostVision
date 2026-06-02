using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public class GetStoreListUseCase(IUnitOfWork unitOfWork) : IGetStoreListUseCase
    {
        private const int MIN_PAGE = 1;
        private const int MIN_PAGE_SIZE = 1;
        private const int MAX_PAGE_SIZE = 100;
        private const string SORT_BY_NAME = "name";
        private const string SORT_BY_RECEIPT_COUNT = "receiptCount";
        private const string SORT_DIRECTION_DESC = "desc";

        public async Task<ServiceResult<StoreListDto>> ExecuteAsync(GetStoreListRequest request, Guid currentUserId, CancellationToken ct)
        {
            int page = Math.Max(request.Page, MIN_PAGE);
            int pageSize = Math.Clamp(request.PageSize, MIN_PAGE_SIZE, MAX_PAGE_SIZE);
            string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
            Expression<Func<Store, bool>> predicate = store =>
                store.Receipts.Any(receipt =>
                    receipt.CreatedByUserId == currentUserId ||
                    receipt.Accounts.Any(link =>
                        link.Account != null &&
                        (
                            link.Account.CreatedByUserId == currentUserId ||
                            link.Account.Members.Any(member => member.UserId == currentUserId)
                        ))) &&
                (search == null ||
                 store.Name.Contains(search) ||
                 store.Address.Contains(search) ||
                 (store.AdaptiveName != null && store.AdaptiveName.Contains(search)));

            List<Store> stores = await unitOfWork.Store.GetItemsByPredicateAsync(
                predicate,
                asNoTracking: true,
                include: query => query
                    .Include(store => store.Receipts)
                    .ThenInclude(receipt => receipt.Accounts)
                    .ThenInclude(link => link.Account)
                    .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<StoreListItemDto> allItems = GroupStoreItems(stores, request.UseAdaptiveNames, currentUserId);
            int totalCount = allItems.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            int skip = (page - 1) * pageSize;

            List<StoreListItemDto> items = SortItems(allItems, request.SortBy, request.SortDirection)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            StoreListDto result = new()
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > MIN_PAGE,
                HasNextPage = page < totalPages
            };

            return ServiceResult<StoreListDto>.Ok(result);
        }

        private static List<StoreListItemDto> SortItems(List<StoreListItemDto> items, string? sortBy, string? sortDirection)
        {
            bool isDescending = string.Equals(sortDirection, SORT_DIRECTION_DESC, StringComparison.OrdinalIgnoreCase);
            string normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? SORT_BY_NAME : sortBy.Trim();

            IOrderedEnumerable<StoreListItemDto> orderedItems = normalizedSortBy switch
            {
                SORT_BY_RECEIPT_COUNT => isDescending
                    ? items.OrderByDescending(item => item.ReceiptCount).ThenBy(item => item.Name).ThenBy(item => item.Address)
                    : items.OrderBy(item => item.ReceiptCount).ThenBy(item => item.Name).ThenBy(item => item.Address),
                _ => isDescending
                    ? items.OrderByDescending(item => item.Name).ThenBy(item => item.Address).ThenByDescending(item => item.ReceiptCount)
                    : items.OrderBy(item => item.Name).ThenBy(item => item.Address).ThenByDescending(item => item.ReceiptCount)
            };

            return orderedItems.ToList();
        }

        private static List<StoreListItemDto> GroupStoreItems(List<Store> stores, bool useAdaptiveNames, Guid currentUserId)
        {
            return stores
                .Select(store => new
                {
                    Store = store,
                    Item = CreateStoreListItem(store, useAdaptiveNames, currentUserId)
                })
                .GroupBy(item => item.Store.NormalizedName)
                .Select(group =>
                {
                    List<StoreListItemDto> children = group.Select(item => item.Item).ToList();
                    StoreListItemDto firstItem = children.First();

                    if (children.Count == 1)
                        return firstItem;

                    return new StoreListItemDto
                    {
                        Id = firstItem.Id,
                        Name = firstItem.Name,
                        Address = firstItem.Address,
                        AdaptiveName = firstItem.AdaptiveName,
                        DisplayName = firstItem.DisplayName,
                        ReceiptCount = children.Sum(item => item.ReceiptCount),
                        GroupKey = group.Key,
                        Children = children
                    };
                })
                .ToList();
        }

        private static StoreListItemDto CreateStoreListItem(Store store, bool useAdaptiveNames, Guid currentUserId)
        {
            return new StoreListItemDto
            {
                Id = store.Id,
                Name = store.Name,
                Address = store.Address,
                AdaptiveName = store.AdaptiveName,
                DisplayName = useAdaptiveNames && !string.IsNullOrWhiteSpace(store.AdaptiveName)
                    ? store.AdaptiveName
                    : store.Name,
                ReceiptCount = CountAccessibleReceipts(store, currentUserId),
                GroupKey = store.NormalizedName
            };
        }

        private static int CountAccessibleReceipts(Store store, Guid currentUserId)
        {
            return store.Receipts
                .Where(receipt => IsReceiptAccessible(receipt, currentUserId))
                .Select(receipt => receipt.Id)
                .Distinct()
                .Count();
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
