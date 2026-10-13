using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using CostVision.Application.Extensions;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public class GetStoreListUseCase(IUnitOfWork unitOfWork) : IGetStoreListUseCase
    {
        private const int MIN_PAGE = 1;
        private const int MIN_PAGE_SIZE = 1;
        private const int MAX_PAGE_SIZE = 100;
        private const string SORT_BY_NAME = "name";
        private const string SORT_BY_RECEIPT_COUNT = "receiptCount";
        private const string SORT_BY_TOTAL_SPENT = "totalSpent";
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

            List<StoreListItemDto> allItems = request.GroupByName
                ? GroupStoreItems(stores, request, currentUserId)
                : stores.Select(store => CreateStoreListItem(store, request, currentUserId)).Where(item => item.ReceiptCount > 0).ToList();
            int totalCount = allItems.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            int skip = (page - 1) * pageSize;

            List<StoreListItemDto> items = SortItems(allItems, request.SortBy, request.SortDirection)
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            List<ReceiptIdentity> documents = stores.SelectMany(store => store.Receipts)
                .Where(receipt => IsReceiptAccessible(receipt, request, currentUserId))
                .Select(receipt => receipt.GetIdentity()).Distinct().ToList();
            var totals = ReceiptFinancialRules.CalculateTotals(documents.Select(identity => (identity.OperationType, identity.TotalSum)));

            StoreListDto result = new()
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > MIN_PAGE,
                HasNextPage = page < totalPages,
                TotalSum = totals.Expense,
                IncomeSum = totals.Income
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
                SORT_BY_TOTAL_SPENT => isDescending
                    ? items.OrderByDescending(item => item.TotalSpent).ThenBy(item => item.Name).ThenBy(item => item.Address)
                    : items.OrderBy(item => item.TotalSpent).ThenBy(item => item.Name).ThenBy(item => item.Address),
                _ => isDescending
                    ? items.OrderByDescending(item => item.Name).ThenBy(item => item.Address).ThenByDescending(item => item.ReceiptCount)
                    : items.OrderBy(item => item.Name).ThenBy(item => item.Address).ThenByDescending(item => item.ReceiptCount)
            };

            return orderedItems.ToList();
        }

        private static List<StoreListItemDto> GroupStoreItems(List<Store> stores, GetStoreListRequest request, Guid currentUserId)
        {
            return stores
                .Select(store => new
                {
                    Store = store,
                    Item = CreateStoreListItem(store, request, currentUserId)
                })
                .GroupBy(item => item.Store.NormalizedName)
                .Select(group =>
                {
                    List<StoreListItemDto> children = group.Select(item => item.Item).ToList();
                    StoreListItemDto firstItem = children.First();

                    if (children.Count == 1)
                    {
                        return firstItem;
                    }

                    List<ReceiptIdentity> documents = group.SelectMany(item => item.Store.Receipts)
                        .Where(receipt => IsReceiptAccessible(receipt, request, currentUserId))
                        .Select(receipt => receipt.GetIdentity()).Distinct().ToList();
                    var totals = ReceiptFinancialRules.CalculateTotals(documents.Select(identity => (identity.OperationType, identity.TotalSum)));

                    return new StoreListItemDto
                    {
                        Id = firstItem.Id,
                        Name = firstItem.Name,
                        Address = "Несколько адресов",
                        AdaptiveName = firstItem.AdaptiveName,
                        DisplayName = firstItem.DisplayName,
                        ReceiptCount = documents.Count,
                        TotalSpent = totals.Expense,
                        IncomeSum = totals.Income,
                        GroupKey = group.Key,
                        Children = children
                    };
                })
                .Where(item => item.ReceiptCount > 0)
                .ToList();
        }

        private static StoreListItemDto CreateStoreListItem(Store store, GetStoreListRequest request, Guid currentUserId)
        {
            List<ReceiptIdentity> documents = store.Receipts
                .Where(receipt => IsReceiptAccessible(receipt, request, currentUserId))
                .Select(receipt => receipt.GetIdentity()).Distinct().ToList();
            var totals = ReceiptFinancialRules.CalculateTotals(documents.Select(identity => (identity.OperationType, identity.TotalSum)));
            string displayName = store.Name;
            if (request.UseAdaptiveNames && !string.IsNullOrWhiteSpace(store.AdaptiveName))
            {
                displayName = store.AdaptiveName;
            }

            return new StoreListItemDto
            {
                Id = store.Id, Name = store.Name, Address = store.Address,
                AdaptiveName = store.AdaptiveName, DisplayName = displayName,
                ReceiptCount = documents.Count, TotalSpent = totals.Expense, IncomeSum = totals.Income,
                GroupKey = store.NormalizedName
            };
        }

        private static bool IsReceiptAccessible(Receipt receipt, GetStoreListRequest request, Guid currentUserId)
        {
            bool beforePeriod = request.DateFrom.HasValue && receipt.DateTime.Date < request.DateFrom.Value.Date;
            bool afterPeriod = request.DateTo.HasValue && receipt.DateTime.Date > request.DateTo.Value.Date;
            bool matchesAccount = !request.AccountId.HasValue || receipt.Accounts.Any(link =>
                link.AccountId == request.AccountId.Value && FinancialDataAccess.CanReadAccount(link.Account, currentUserId));
            if (beforePeriod || afterPeriod || !matchesAccount)
            {
                return false;
            }

            return FinancialDataAccess.CanReadReceipt(receipt, currentUserId);
        }
    }
}
