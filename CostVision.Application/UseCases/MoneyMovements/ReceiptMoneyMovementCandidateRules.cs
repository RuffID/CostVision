using System.Linq.Expressions;
using CostVision.Application.Extensions;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements;

internal static class ReceiptMoneyMovementCandidateRules
{
    public static Expression<Func<MoneyMovement, bool>> BuildPredicate(
        Receipt receipt, Guid currentUserId, DateTime? dateFrom, DateTime? dateToExclusive,
        bool useAmountFilter, decimal amountTolerance, bool excludeLinked)
    {
        List<Guid> accountIds = receipt.Accounts.Select(link => link.AccountId).ToList();
        bool ownWithoutAccount = accountIds.Count == 0 && receipt.CreatedByUserId == currentUserId;
        Expression<Func<MoneyMovement, bool>> predicate = movement =>
            (!dateFrom.HasValue || movement.OccurredAt >= dateFrom.Value) &&
            (!dateToExclusive.HasValue || movement.OccurredAt < dateToExclusive.Value) &&
            (!useAmountFilter || Math.Abs(movement.Amount - receipt.TotalSum) <= amountTolerance) &&
            (!excludeLinked || !movement.ReceiptLinks.Any()) &&
            (accountIds.Contains(movement.AccountId) ||
             (ownWithoutAccount && movement.CreatedByUserId == currentUserId));

        return predicate.WithAccountReadAccess(movement => movement.Account, currentUserId);
    }

    public static Expression<Func<MoneyMovement, bool>> ForInitialModal(Receipt receipt, Guid currentUserId)
    {
        return BuildPredicate(receipt, currentUserId, receipt.DateTime.Date.AddDays(-1),
            receipt.DateTime.Date.AddDays(2), true, 1m, true);
    }
}
