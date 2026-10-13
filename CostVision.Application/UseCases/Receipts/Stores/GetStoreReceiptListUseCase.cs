using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using CostVision.Application.Extensions;
using CostVision.Application.UseCases.Receipts.Receipts;

namespace CostVision.Application.UseCases.Receipts.Stores
{
    public class GetStoreReceiptListUseCase(IUnitOfWork unitOfWork) : IGetStoreReceiptListUseCase
    {
        private const int MIN_PAGE = 1;
        private const int MIN_PAGE_SIZE = 1;
        private const int MAX_PAGE_SIZE = 100;

        public async Task<ServiceResult<StoreReceiptListDto>> ExecuteAsync(GetStoreReceiptListRequest request, Guid currentUserId, CancellationToken ct)
        {
            int page = Math.Max(request.Page, MIN_PAGE);
            int pageSize = Math.Clamp(request.PageSize, MIN_PAGE_SIZE, MAX_PAGE_SIZE);
            Guid? storeId = request.StoreId.HasValue && request.StoreId.Value != Guid.Empty ? request.StoreId.Value : null;
            string? groupKey = string.IsNullOrWhiteSpace(request.GroupKey) ? null : request.GroupKey.Trim();

            if (!storeId.HasValue && groupKey == null)
            {
                return ServiceResult<StoreReceiptListDto>.Fail(ServiceErrorType.Validation, "Не задан магазин для просмотра чеков.");
            }

            Expression<Func<Receipt, bool>> predicate = receipt =>
                receipt.Store != null &&
                ((storeId.HasValue && receipt.StoreId == storeId.Value) ||
                 (groupKey != null && receipt.Store.NormalizedName == groupKey));

            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                predicate.WithReceiptReadAccess(receipt => receipt, currentUserId),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Store)
                    .Include(receipt => receipt.CreatedByUser)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                        .ThenInclude(link => link.MoneyMovement)
                            .ThenInclude(movement => movement!.Account)
                                .ThenInclude(account => account!.Members)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptDto> allItems = receipts
                .GroupBy(receipt => receipt.GetIdentity())
                .Select(receiptGroup => receiptGroup.MapReceiptGroupDto(currentUserId))
                .OrderByDescending(receipt => receipt.DateTime)
                .ToList();

            await ReceiptReportCandidateCounts.FillAsync(unitOfWork, allItems, currentUserId, ct);

            int totalCount = allItems.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            int skip = (page - 1) * pageSize;
            var totals = ReceiptFinancialRules.CalculateTotals(allItems.Select(receipt => (receipt.OperationType, receipt.TotalSum)));

            StoreReceiptListDto result = new()
            {
                Items = allItems
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > MIN_PAGE,
                HasNextPage = page < totalPages,
                TotalSum = totals.Expense,
                IncomeSum = totals.Income
            };

            return ServiceResult<StoreReceiptListDto>.Ok(result);
        }
    }
}
