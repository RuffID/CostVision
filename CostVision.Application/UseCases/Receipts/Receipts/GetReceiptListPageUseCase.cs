using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class GetReceiptListPageUseCase(
        IGetReceiptListUseCase getReceiptListUseCase,
        IUnitOfWork unitOfWork) : IGetReceiptListPageUseCase
    {
        public async Task<ServiceResult<ReceiptListDto>> ExecuteAsync(User currentUser, GetReceiptListRequest request, CancellationToken ct)
        {
            ServiceResult<List<ReceiptDto>> receiptListResult = await getReceiptListUseCase.ExecuteAsync(currentUser, request.DateFrom, request.DateTo, ct);

            if (!receiptListResult.Success || receiptListResult.Data == null)
                return ServiceResult<ReceiptListDto>.Fail(receiptListResult.Error!.StatusCode, receiptListResult.Error.Message);

            List<ReceiptDto> receipts = receiptListResult.Data;
            receipts = ApplyFilters(receipts, request);

            int pageSize = Math.Clamp(request.PageSize, 1, 100);
            int totalCount = receipts.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            int page = Math.Min(Math.Max(request.Page, 1), totalPages);
            decimal totalSum = receipts.Sum(receipt => receipt.TotalSum);

            if (receipts.Count == 0)
                return ServiceResult<ReceiptListDto>.Ok(new ReceiptListDto { Page = page, PageSize = pageSize, TotalPages = totalPages });

            DateTime periodStart = request.DateFrom.Date;
            DateTime periodEnd = request.DateTo.Date.AddDays(1);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= periodStart &&
                            movement.OccurredAt < periodEnd &&
                            !movement.ReceiptLinks.Any() &&
                            (movement.CreatedByUserId == currentUser.Id ||
                             movement.Account!.CreatedByUserId == currentUser.Id ||
                             movement.Account.Members.Any(member => member.UserId == currentUser.Id)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                    .AsSplitQuery(),
                ct: ct);

            List<ReceiptDto> pageReceipts = receipts.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            Dictionary<MovementAccountMatchKey, int> movementsByAccount = BuildMovementsByAccount(movements);
            Dictionary<MovementOwnerMatchKey, int> movementsByOwner = BuildMovementsByOwner(movements);
            foreach (ReceiptDto receipt in pageReceipts)
                receipt.AvailableMoneyMovementCount = CountAvailableMoneyMovements(receipt, movementsByAccount, movementsByOwner, currentUser.Id);

            return ServiceResult<ReceiptListDto>.Ok(new ReceiptListDto
            {
                Items = pageReceipts,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages,
                TotalSum = totalSum
            });
        }

        private static List<ReceiptDto> ApplyFilters(List<ReceiptDto> receipts, GetReceiptListRequest request)
        {
            IEnumerable<ReceiptDto> query = receipts;

            if (request.AccountId.HasValue)
                query = query.Where(receipt => receipt.Accounts.Any(account => account.Id == request.AccountId.Value));

            string search = request.Search?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(search))
            {
                string normalizedSearch = search.ToLowerInvariant();
                query = query.Where(receipt => MatchesSearch(receipt, normalizedSearch, request.SearchMode));
            }

            query = request.OperationFilter switch
            {
                "withoutOperations" => query.Where(receipt => receipt.MoneyMovementCount == 0),
                "withOperations" => query.Where(receipt => receipt.MoneyMovementCount > 0),
                "amountMismatch" => query.Where(receipt => receipt.MoneyMovementCount > 0 && Math.Abs(receipt.TotalSum - receipt.MoneyMovementsTotalSum) >= 0.01m),
                _ => query
            };

            return query.ToList();
        }

        private static bool MatchesSearch(ReceiptDto receipt, string search, string? mode)
        {
            string store = receipt.RetailPlace.ToLowerInvariant();
            string accounts = string.Join(" ", receipt.Accounts.Select(account => account.Name)).ToLowerInvariant();
            return mode switch
            {
                "sum" => receipt.TotalSum.ToString().Contains(search),
                "shop" => store.Contains(search) || accounts.Contains(search),
                "fn" => receipt.FiscalDriveNumber.ToLowerInvariant().Contains(search),
                "fd" => receipt.FiscalDocumentNumber.ToLowerInvariant().Contains(search),
                "fp" => receipt.FiscalSign.ToLowerInvariant().Contains(search),
                _ => store.Contains(search) || accounts.Contains(search) || receipt.TotalSum.ToString().Contains(search) || receipt.FiscalDriveNumber.ToLowerInvariant().Contains(search) || receipt.FiscalDocumentNumber.ToLowerInvariant().Contains(search) || receipt.FiscalSign.ToLowerInvariant().Contains(search)
            };
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
