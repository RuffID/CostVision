using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.MoneyMovements.Helpers;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetMoneyMovementListUseCase(IUnitOfWork unitOfWork) : IGetMoneyMovementListUseCase
    {
        public async Task<ServiceResult<List<MoneyMovementDto>>> ExecuteAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct)
        {
            if (dateTo < dateFrom)
                return ServiceResult<List<MoneyMovementDto>>.Fail(400, "Дата окончания периода не может быть меньше даты начала.");

            DateTime periodEnd = dateTo.Date.AddDays(1);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= dateFrom.Date &&
                            movement.OccurredAt < periodEnd &&
                            (!accountId.HasValue || movement.AccountId == accountId.Value) &&
                            (movement.CreatedByUserId == currentUserId ||
                             movement.Account!.CreatedByUserId == currentUserId ||
                             movement.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.PerformedByUser)
                    .Include(movement => movement.ReceiptLinks)
                        .ThenInclude(link => link.Receipt)
                            .ThenInclude(receipt => receipt!.Store)
                    .AsSplitQuery(),
                ct: ct);

            List<Receipt> availableReceipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.DateTime >= dateFrom.Date &&
                           receipt.DateTime < periodEnd &&
                           !receipt.MoneyMovementLinks.Any() &&
                           (receipt.CreatedByUserId == currentUserId ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Store)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                    .AsSplitQuery(),
                ct: ct);

            Dictionary<ReceiptAccountMatchKey, int> receiptsByAccount = BuildReceiptsByAccount(availableReceipts);
            Dictionary<ReceiptWithoutAccountMatchKey, int> receiptsWithoutAccount = BuildReceiptsWithoutAccount(availableReceipts);
            List<MoneyMovementDto> result = movements
                .OrderByDescending(movement => movement.OccurredAt)
                .Select(movement => movement.MapDto(GetAvailableReceiptCount(movement, receiptsByAccount, receiptsWithoutAccount)))
                .ToList();

            return ServiceResult<List<MoneyMovementDto>>.Ok(result);
        }

        private static Dictionary<ReceiptAccountMatchKey, int> BuildReceiptsByAccount(List<Receipt> receipts)
        {
            Dictionary<ReceiptAccountMatchKey, int> receiptsByAccount = new();

            foreach (Receipt receipt in receipts)
            {
                foreach (ReceiptAccount accountLink in receipt.Accounts)
                {
                    ReceiptAccountMatchKey key = new(receipt.DateTime.Date, receipt.TotalSum, accountLink.AccountId);
                    receiptsByAccount.TryGetValue(key, out int count);
                    receiptsByAccount[key] = count + 1;
                }
            }

            return receiptsByAccount;
        }

        private static Dictionary<ReceiptWithoutAccountMatchKey, int> BuildReceiptsWithoutAccount(List<Receipt> receipts)
        {
            Dictionary<ReceiptWithoutAccountMatchKey, int> receiptsWithoutAccount = new();

            foreach (Receipt receipt in receipts)
            {
                if (receipt.Accounts.Count != 0)
                    continue;

                ReceiptWithoutAccountMatchKey key = new(receipt.DateTime.Date, receipt.TotalSum);
                receiptsWithoutAccount.TryGetValue(key, out int count);
                receiptsWithoutAccount[key] = count + 1;
            }

            return receiptsWithoutAccount;
        }

        private static int GetAvailableReceiptCount(
            MoneyMovement movement,
            Dictionary<ReceiptAccountMatchKey, int> receiptsByAccount,
            Dictionary<ReceiptWithoutAccountMatchKey, int> receiptsWithoutAccount)
        {
            if (movement.AccountId == Guid.Empty)
            {
                ReceiptWithoutAccountMatchKey withoutAccountKey = new(movement.OccurredAt.Date, movement.Amount);
                return receiptsWithoutAccount.GetValueOrDefault(withoutAccountKey);
            }

            ReceiptAccountMatchKey accountKey = new(movement.OccurredAt.Date, movement.Amount, movement.AccountId);
            return receiptsByAccount.GetValueOrDefault(accountKey);
        }

        private readonly record struct ReceiptAccountMatchKey(DateTime Date, decimal Amount, Guid AccountId);

        private readonly record struct ReceiptWithoutAccountMatchKey(DateTime Date, decimal Amount);
    }
}
