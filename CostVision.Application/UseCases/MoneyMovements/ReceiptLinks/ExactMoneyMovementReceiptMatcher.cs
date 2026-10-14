using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Extensions;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.MoneyMovements;

internal static class ExactMoneyMovementReceiptMatcher
{
    // Все чтения выполняются вызывающим сценарием внутри Serializable до записи связей.
    public static async Task<List<MoneyMovementReceipt>> LoadAndBuildAsync(
        IMoneyMovementRepository moneyMovementRepository, IReceiptRepository receiptRepository, Guid currentUserId, DateTime periodStart, DateTime periodEnd,
        Guid? accountId, Guid? onlyMovementId, CancellationToken ct)
    {
        List<MoneyMovement> movements = await moneyMovementRepository.GetItemsByPredicateAsync(
            movement => movement.OccurredAt >= periodStart && movement.OccurredAt < periodEnd &&
                movement.Type == MoneyMovementType.Expense && !movement.ReceiptLinks.Any() &&
                (!accountId.HasValue || movement.AccountId == accountId.Value) &&
                movement.Account!.Members.Any(member => member.UserId == currentUserId &&
                    (member.Role == AccountAccessRole.Owner || member.Role == AccountAccessRole.Editor)),
            asNoTracking: true,
            include: query => query.Include(movement => movement.Account).ThenInclude(account => account!.Members)
                .Include(movement => movement.ReceiptLinks).AsSplitQuery(), ct: ct);

        if (movements.Count == 0)
        {
            return [];
        }

        Expression<Func<Receipt, bool>> receiptPredicate = receipt =>
            receipt.DateTime >= periodStart && receipt.DateTime < periodEnd &&
            receipt.OperationType == ReceiptOperationType.Income && !receipt.MoneyMovementLinks.Any() &&
            (!accountId.HasValue || receipt.Accounts.Any(link => link.AccountId == accountId.Value));
        List<Receipt> receipts = await receiptRepository.GetItemsByPredicateAsync(
            receiptPredicate.WithReceiptReadAccess(receipt => receipt, currentUserId), asNoTracking: true,
            include: query => query.Include(receipt => receipt.Accounts).ThenInclude(link => link.Account)
                .ThenInclude(account => account!.Members).Include(receipt => receipt.MoneyMovementLinks).AsSplitQuery(), ct: ct);

        List<MoneyMovementReceipt> links = new();
        foreach (MoneyMovement movement in movements)
        {
            if (onlyMovementId.HasValue && movement.Id != onlyMovementId.Value)
            {
                continue;
            }

            List<Receipt> candidates = receipts.Where(receipt => Matches(movement, receipt)).ToList();
            if (candidates.Count != 1)
            {
                continue;
            }

            Receipt receipt = candidates[0];
            // Проверяем исходный набор; не превращаем неоднозначность в выбор порядком обхода.
            if (movements.Count(candidate => Matches(candidate, receipt)) != 1)
            {
                continue;
            }

            if (!MoneyMovementReceipt.TryCreate(movement.Id, receipt.Id, currentUserId, DateTime.UtcNow,
                    out MoneyMovementReceipt? link, out string? error))
            {
                throw new InvalidOperationException(error);
            }

            links.Add(link!);
        }

        return links;
    }

    private static bool Matches(MoneyMovement movement, Receipt receipt)
    {
        return movement.OccurredAt.Date == receipt.DateTime.Date && movement.Amount == receipt.TotalSum &&
            receipt.Accounts.Any(link => link.AccountId == movement.AccountId);
    }
}
