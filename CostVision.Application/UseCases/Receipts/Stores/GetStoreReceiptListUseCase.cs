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
                return ServiceResult<StoreReceiptListDto>.Fail(ServiceErrorType.Validation, "Не задан магазин для просмотра чеков.");

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
                    .Include(receipt => receipt.CreatedByUser)
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

            Dictionary<MovementAccountMatchKey, int> movementsByAccount = BuildMovementsByAccount(movements);
            Dictionary<MovementOwnerMatchKey, int> movementsByOwner = BuildMovementsByOwner(movements);
            foreach (ReceiptDto receipt in receipts)
                receipt.AvailableMoneyMovementCount = CountAvailableMoneyMovements(receipt, movementsByAccount, movementsByOwner, currentUserId);
        }

        private static Dictionary<MovementAccountMatchKey, int> BuildMovementsByAccount(List<MoneyMovement> movements)
        {
            Dictionary<MovementAccountMatchKey, int> movementsByAccount = new();

            foreach (MoneyMovement movement in movements)
            {
                MovementAccountMatchKey key = new(movement.OccurredAt.Date, movement.Amount, movement.AccountId);
                movementsByAccount.TryGetValue(key, out int count);
                movementsByAccount[key] = count + 1;
            }

            return movementsByAccount;
        }

        private static Dictionary<MovementOwnerMatchKey, int> BuildMovementsByOwner(List<MoneyMovement> movements)
        {
            Dictionary<MovementOwnerMatchKey, int> movementsByOwner = new();

            foreach (MoneyMovement movement in movements)
            {
                MovementOwnerMatchKey key = new(movement.OccurredAt.Date, movement.Amount, movement.CreatedByUserId);
                movementsByOwner.TryGetValue(key, out int count);
                movementsByOwner[key] = count + 1;
            }

            return movementsByOwner;
        }

        private static int CountAvailableMoneyMovements(
            ReceiptDto receipt,
            Dictionary<MovementAccountMatchKey, int> movementsByAccount,
            Dictionary<MovementOwnerMatchKey, int> movementsByOwner,
            Guid currentUserId)
        {
            if (receipt.Accounts.Count == 0)
            {
                MovementOwnerMatchKey ownerKey = new(receipt.DateTime.Date, receipt.TotalSum, currentUserId);
                return movementsByOwner.GetValueOrDefault(ownerKey);
            }

            int count = 0;
            foreach (ReceiptAccountDto account in receipt.Accounts)
            {
                MovementAccountMatchKey accountKey = new(receipt.DateTime.Date, receipt.TotalSum, account.Id);
                count += movementsByAccount.GetValueOrDefault(accountKey);
            }

            return count;
        }

        private readonly record struct MovementAccountMatchKey(DateTime Date, decimal Amount, Guid AccountId);

        private readonly record struct MovementOwnerMatchKey(DateTime Date, decimal Amount, Guid OwnerUserId);
    }
}
