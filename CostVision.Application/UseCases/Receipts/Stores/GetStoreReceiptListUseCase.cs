using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
                return ServiceResult<StoreReceiptListDto>.Fail(400, "Не задан магазин для просмотра чеков.");

            Expression<Func<Receipt, bool>> predicate = receipt =>
                receipt.Store != null &&
                ((storeId.HasValue && receipt.StoreId == storeId.Value) ||
                 (groupKey != null && receipt.Store.NormalizedName == groupKey)) &&
                (receipt.CreatedByUserId == currentUserId ||
                 receipt.Accounts.Any(link =>
                     link.Account != null &&
                     (link.Account.CreatedByUserId == currentUserId ||
                      link.Account.Members.Any(member => member.UserId == currentUserId))));

            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                predicate,
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Store)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                        .ThenInclude(link => link.MoneyMovement)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptDto> allItems = receipts
                .GroupBy(receipt => receipt.GetIdentityKey())
                .Select(receiptGroup => receiptGroup.MapReceiptGroupDto(currentUserId))
                .OrderByDescending(receipt => receipt.DateTime)
                .ToList();

            await FillAvailableMoneyMovementCountsAsync(allItems, currentUserId, ct);

            int totalCount = allItems.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(page, totalPages);
            int skip = (page - 1) * pageSize;

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
                HasNextPage = page < totalPages
            };

            return ServiceResult<StoreReceiptListDto>.Ok(result);
        }

        private async Task FillAvailableMoneyMovementCountsAsync(List<ReceiptDto> receipts, Guid currentUserId, CancellationToken ct)
        {
            if (receipts.Count == 0)
                return;

            DateTime periodStart = receipts.Min(receipt => receipt.DateTime).Date;
            DateTime periodEnd = receipts.Max(receipt => receipt.DateTime).Date.AddDays(1);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= periodStart &&
                            movement.OccurredAt < periodEnd &&
                            !movement.ReceiptLinks.Any() &&
                            (movement.CreatedByUserId == currentUserId ||
                             movement.Account!.CreatedByUserId == currentUserId ||
                             movement.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                    .AsSplitQuery(),
                ct: ct);

            foreach (ReceiptDto receipt in receipts)
            {
                receipt.AvailableMoneyMovementCount = CountAvailableMoneyMovements(receipt, movements, currentUserId);
            }
        }

        private static int CountAvailableMoneyMovements(ReceiptDto receipt, List<MoneyMovement> movements, Guid currentUserId)
        {
            DateTime receiptDate = receipt.DateTime.Date;
            List<Guid> receiptAccountIds = receipt.Accounts.Select(account => account.Id).ToList();

            return movements.Count(movement =>
                movement.OccurredAt.Date == receiptDate &&
                movement.Amount == receipt.TotalSum &&
                (receiptAccountIds.Contains(movement.AccountId) ||
                 (receiptAccountIds.Count == 0 && movement.CreatedByUserId == currentUserId)));
        }
    }
}
