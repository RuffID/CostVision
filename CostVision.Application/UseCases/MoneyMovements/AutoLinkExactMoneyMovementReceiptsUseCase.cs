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
                return ServiceResult<int>.Fail(400, "Дата окончания периода не может быть меньше даты начала.");

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

            await unitOfWork.ExecuteInTransaction(() =>
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
            DateTime createdAtUtc = DateTime.UtcNow;

            foreach (MoneyMovement movement in movements)
            {
                if (linkedMovementIds.Contains(movement.Id))
                    continue;

                List<Receipt> receiptCandidates = receipts
                    .Where(receipt => !linkedReceiptIds.Contains(receipt.Id) && IsExactMatch(movement, receipt))
                    .ToList();

                if (receiptCandidates.Count != 1)
                    continue;

                Receipt receipt = receiptCandidates[0];
                int movementCandidateCount = movements.Count(candidate =>
                    !linkedMovementIds.Contains(candidate.Id) &&
                    IsExactMatch(candidate, receipt));

                if (movementCandidateCount != 1)
                    continue;

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

        private static bool IsExactMatch(MoneyMovement movement, Receipt receipt)
        {
            return movement.Amount == receipt.TotalSum &&
                   movement.OccurredAt.Date == receipt.DateTime.Date &&
                   receipt.Accounts.Any(link => link.AccountId == movement.AccountId);
        }
    }
}
