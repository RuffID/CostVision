using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Stores;

public class GetStoreListUseCase(IStoreRepository storeRepository) : IGetStoreListUseCase
{
    public async Task<ServiceResult<StoreListDto>> ExecuteAsync(GetStoreListRequest request, Guid currentUserId, CancellationToken ct)
    {
        ServiceResult requestShape = RequestShapeValidation.Validate(request);
        if (!requestShape.Success)
        {
            return requestShape.PropagateFailure<StoreListDto>();
        }

        ServiceResult dateRange = RequestDateValidation.ValidateCalendarRange(request.DateFrom, request.DateTo);
        if (!dateRange.Success)
        {
            return dateRange.PropagateFailure<StoreListDto>();
        }

        DateTime? start = request.DateFrom?.Date;
        DateTime? end = request.DateTo?.Date.AddDays(1);
        Expression<Func<Receipt, bool>> receiptPredicate = receipt =>
            (!start.HasValue || receipt.DateTime >= start.Value) && (!end.HasValue || receipt.DateTime < end.Value) &&
            (!request.AccountId.HasValue || receipt.Accounts.Any(link =>
                link.AccountId == request.AccountId.Value && link.Account != null &&
                (link.Account.CreatedByUserId == currentUserId || link.Account.Members.Any(member => member.UserId == currentUserId))));
        receiptPredicate = receiptPredicate.WithReceiptReadAccess(receipt => receipt, currentUserId);
        string search = request.Search?.Trim() ?? "";
        Expression<Func<Store, bool>> predicate = store => store.Receipts.AsQueryable().Any(receiptPredicate) &&
            (search == "" || store.Name.Contains(search) || store.Address.Contains(search) ||
             (store.AdaptiveName != null && store.AdaptiveName.Contains(search)));
        List<int> counts = await storeRepository.GetProjectedItemsAsync(predicate,
            query => StoreReadQueries.Representatives(query, request.GroupByName).GroupBy(store => 1).Select(group => group.Count()), ct);
        List<ReadQueryTotals> summaries = await storeRepository.GetProjectedItemsAsync(predicate,
            query => ReceiptReadQueries.Totals(ReceiptReadQueries.Documents(query.SelectMany(store => store.Receipts).Where(receiptPredicate))), ct);
        int count = counts.SingleOrDefault();
        ReadQueryTotals totals = ReceiptReadQueries.GetTotals(summaries);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        int totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
        int page = Math.Clamp(request.Page, 1, totalPages);
        List<StoreListItemDto> items = await storeRepository.GetProjectedItemsAsync(predicate,
            query => StoreReadQueries.Sort(StoreReadQueries.Project(query, receiptPredicate, request.GroupByName, request.UseAdaptiveNames),
                request.SortBy, request.SortDirection).Skip((page - 1) * pageSize).Take(pageSize), ct);
        if (request.GroupByName && items.Count > 0)
        {
            List<string> keys = items.Select(item => item.GroupKey).ToList();
            List<StoreListItemDto> children = await storeRepository.GetProjectedItemsAsync(predicate,
                query => StoreReadQueries.Project(query.Where(store => keys.Contains(store.NormalizedName)),
                    receiptPredicate, false, request.UseAdaptiveNames).OrderBy(item => item.Id), ct);
            foreach (StoreListItemDto item in items)
            {
                List<StoreListItemDto> group = children.Where(child => child.GroupKey == item.GroupKey).ToList();
                if (group.Count > 1)
                {
                    item.Address = "Несколько адресов";
                    item.Children = group;
                }
            }
        }

        return ServiceResult<StoreListDto>.Ok(new StoreListDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = count, TotalPages = totalPages,
            HasPreviousPage = page > 1, HasNextPage = page < totalPages, TotalSum = totals.Expense, IncomeSum = totals.Income
        });
    }
}
