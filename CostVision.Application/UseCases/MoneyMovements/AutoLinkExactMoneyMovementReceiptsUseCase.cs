using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class AutoLinkExactMoneyMovementReceiptsUseCase(IUnitOfWork unitOfWork) : IAutoLinkExactMoneyMovementReceiptsUseCase
    {
        public async Task<ServiceResult<int>> ExecuteAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct)
        {
            if (dateTo.Date < dateFrom.Date)
                return ServiceResult<int>.Fail(ServiceErrorType.Validation, "Дата окончания периода не может быть меньше даты начала.");

            DateTime periodStart = dateFrom.Date;
            DateTime periodEnd = dateTo.Date.AddDays(1);

            List<MoneyMovement> movements = await unitOfWork.MoneyMovement.GetItemsByPredicateAsync(
                movement => movement.OccurredAt >= periodStart &&
                            movement.OccurredAt < periodEnd &&
                            movement.Type == MoneyMovementType.Expense &&
                            !movement.ReceiptLinks.Any() &&
                            (!accountId.HasValue || movement.AccountId == accountId.Value) &&
                            (movement.Account!.CreatedByUserId == currentUserId ||
                             movement.Account.Members.Any(member => member.UserId == currentUserId)),
                asNoTracking: true,
                include: query => query
                    .Include(movement => movement.Account)
                        .ThenInclude(account => account!.Members)
                    .Include(movement => movement.ReceiptLinks)
                    .AsSplitQuery(),
                ct: ct);

            if (movements.Count == 0)
                return ServiceResult<int>.Ok(0);

            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(
                receipt => receipt.DateTime >= periodStart &&
                           receipt.DateTime < periodEnd &&
                           receipt.OperationType == ReceiptOperationType.Income &&
                           !receipt.MoneyMovementLinks.Any() &&
                           (!accountId.HasValue || receipt.Accounts.Any(link => link.AccountId == accountId.Value)) &&
                           (receipt.CreatedByUserId == currentUserId ||
                            receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                            receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))),
                asNoTracking: true,
                include: query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .Include(receipt => receipt.MoneyMovementLinks)
                    .AsSplitQuery(),
                ct: ct);

            List<MoneyMovementReceipt> links = BuildExactLinks(movements, receipts, currentUserId);
            if (links.Count == 0)
                return ServiceResult<int>.Ok(0);

            await unitOfWork.ExecuteInTransaction(_ =>
            {
                unitOfWork.MoneyMovementReceipt.CreateRange(links);
                return Task.CompletedTask;
            }, ct);

            return ServiceResult<int>.Ok(links.Count);
        }

        private static List<MoneyMovementReceipt> BuildExactLinks(List<MoneyMovement> movements, List<Receipt> receipts, Guid currentUserId)
        {
            List<MoneyMovementReceipt> links = new();
            HashSet<Guid> linkedMovementIds = new();
            HashSet<Guid> linkedReceiptIds = new();
            Dictionary<ExactMatchKey, List<MoneyMovement>> movementsByKey = BuildMovementsByKey(movements);
            Dictionary<ExactMatchKey, List<Receipt>> receiptsByKey = BuildReceiptsByKey(receipts);
            DateTime createdAtUtc = DateTime.UtcNow;

            foreach (MoneyMovement movement in movements)
            {
                if (linkedMovementIds.Contains(movement.Id))
                    continue;

                ExactMatchKey key = GetExactMatchKey(movement);
                if (!receiptsByKey.TryGetValue(key, out List<Receipt>? receiptCandidates) ||
                    CountUnlinkedReceipts(receiptCandidates, linkedReceiptIds) != 1)
                    continue;

                if (CountUnlinkedMovements(movementsByKey[key], linkedMovementIds) != 1)
                    continue;

                Receipt receipt = GetUnlinkedReceipt(receiptCandidates, linkedReceiptIds);
                if (!MoneyMovementReceipt.TryCreate(
                        movement.Id,
                        receipt.Id,
                        currentUserId,
                        createdAtUtc,
                        out MoneyMovementReceipt? link,
                        out string? error))
                    throw new InvalidOperationException(error!);

                links.Add(link!);

                linkedMovementIds.Add(movement.Id);
                linkedReceiptIds.Add(receipt.Id);
            }

            return links;
        }

        private static Dictionary<ExactMatchKey, List<MoneyMovement>> BuildMovementsByKey(List<MoneyMovement> movements)
        {
            Dictionary<ExactMatchKey, List<MoneyMovement>> movementsByKey = new();

            foreach (MoneyMovement movement in movements)
            {
                ExactMatchKey key = GetExactMatchKey(movement);
                if (!movementsByKey.TryGetValue(key, out List<MoneyMovement>? groupedMovements))
                {
                    groupedMovements = new List<MoneyMovement>();
                    movementsByKey.Add(key, groupedMovements);
                }

                groupedMovements.Add(movement);
            }

            return movementsByKey;
        }

        private static Dictionary<ExactMatchKey, List<Receipt>> BuildReceiptsByKey(List<Receipt> receipts)
        {
            Dictionary<ExactMatchKey, List<Receipt>> receiptsByKey = new();

            foreach (Receipt receipt in receipts)
            {
                foreach (ReceiptAccount accountLink in receipt.Accounts)
                {
                    ExactMatchKey key = new(receipt.DateTime.Date, receipt.TotalSum, accountLink.AccountId);
                    if (!receiptsByKey.TryGetValue(key, out List<Receipt>? groupedReceipts))
                    {
                        groupedReceipts = new List<Receipt>();
                        receiptsByKey.Add(key, groupedReceipts);
                    }

                    groupedReceipts.Add(receipt);
                }
            }

            return receiptsByKey;
        }

        private static int CountUnlinkedReceipts(List<Receipt> receipts, HashSet<Guid> linkedReceiptIds)
        {
            int count = 0;
            foreach (Receipt receipt in receipts)
            {
                if (!linkedReceiptIds.Contains(receipt.Id))
                    count++;
            }

            return count;
        }

        private static int CountUnlinkedMovements(List<MoneyMovement> movements, HashSet<Guid> linkedMovementIds)
        {
            int count = 0;
            foreach (MoneyMovement movement in movements)
            {
                if (!linkedMovementIds.Contains(movement.Id))
                    count++;
            }

            return count;
        }

        private static Receipt GetUnlinkedReceipt(List<Receipt> receipts, HashSet<Guid> linkedReceiptIds)
        {
            foreach (Receipt receipt in receipts)
            {
                if (!linkedReceiptIds.Contains(receipt.Id))
                    return receipt;
            }

            throw new InvalidOperationException("Не найден чек для однозначной связи операции.");
        }

        private static ExactMatchKey GetExactMatchKey(MoneyMovement movement)
        {
            return new ExactMatchKey(movement.OccurredAt.Date, movement.Amount, movement.AccountId);
        }

        private readonly record struct ExactMatchKey(DateTime Date, decimal Amount, Guid AccountId);
    }
}
