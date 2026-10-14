using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Stores;

public class GetStoreReceiptListUseCase(IReceiptRepository receiptRepository, IMoneyMovementRepository moneyMovementRepository) : IGetStoreReceiptListUseCase
{
    public async Task<ServiceResult<StoreReceiptListDto>> ExecuteAsync(GetStoreReceiptListRequest request, Guid currentUserId, CancellationToken ct)
    {
        Guid? storeId = request.StoreId;
        if (storeId == Guid.Empty)
        {
            storeId = null;
        }

        string? groupKey = request.GroupKey?.Trim();
        if (!storeId.HasValue && string.IsNullOrEmpty(groupKey))
        {
            return ServiceResult<StoreReceiptListDto>.Fail(ServiceErrorType.Validation, "Не задан магазин для просмотра чеков.");
        }

        Expression<Func<Receipt, bool>> predicate = receipt => receipt.Store != null &&
            ((storeId.HasValue && receipt.StoreId == storeId.Value) || (groupKey != null && receipt.Store.NormalizedName == groupKey));
        predicate = predicate.WithReceiptReadAccess(receipt => receipt, currentUserId);
        List<ReadQueryTotals> summaries = await receiptRepository.GetProjectedItemsAsync(predicate,
            query => ReceiptReadQueries.Totals(ReceiptReadQueries.Documents(query)), ct);
        ReadQueryTotals totals = ReceiptReadQueries.GetTotals(summaries);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totals.Count / (double)pageSize));
        int page = Math.Clamp(request.Page, 1, totalPages);
        List<Guid> ids = await receiptRepository.GetProjectedItemsAsync(predicate,
            query => ReceiptReadQueries.PageIds(ReceiptReadQueries.Documents(query), (page - 1) * pageSize, pageSize), ct);
        List<ReceiptDto> items = [];
        if (ids.Count > 0)
        {
            List<Receipt> copies = await receiptRepository.GetProjectedItemsAsync(predicate,
                query => ReceiptReadQueries.WithReportGraph(ReceiptReadQueries.CopiesForPage(query, ids)), ct);
            Dictionary<ReceiptIdentity, int> positions = copies.Where(copy => ids.Contains(copy.Id))
                .ToDictionary(copy => copy.GetIdentity(), copy => ids.IndexOf(copy.Id));
            items = copies.GroupBy(copy => copy.GetIdentity()).OrderBy(group => positions[group.Key])
                .Select(group => group.MapReceiptGroupDto(currentUserId)).ToList();
            ServiceResult candidateCounts = await ReceiptReportCandidateCounts.FillAsync(receiptRepository, moneyMovementRepository, items, currentUserId, ct);
            if (!candidateCounts.Success)
            {
                return candidateCounts.PropagateFailure<StoreReceiptListDto>();
            }
        }

        return ServiceResult<StoreReceiptListDto>.Ok(new StoreReceiptListDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = totals.Count, TotalPages = totalPages,
            HasPreviousPage = page > 1, HasNextPage = page < totalPages, TotalSum = totals.Expense, IncomeSum = totals.Income
        });
    }
}
