using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using System.Linq.Expressions;

namespace CostVision.Application.UseCases.MoneyMovements;

/// <summary>
/// Идентичность строки выписки. ID операции и её размещение в счёте не входят в ключ.
/// </summary>
internal readonly record struct BankStatementRowKey(
    DateTime OccurredAt, decimal Amount, MoneyMovementType Type, string ImportComment)
{
    public static BankStatementRowKey From(MoneyMovement movement)
    {
        return new(movement.OccurredAt, movement.Amount, movement.Type, movement.ImportComment ?? string.Empty);
    }

    public Expression<Func<MoneyMovement, bool>> InAccount(Guid accountId)
    {
        DateTime occurredAt = OccurredAt;
        decimal amount = Amount;
        MoneyMovementType type = Type;
        string importComment = ImportComment;

        return movement => movement.AccountId == accountId &&
            movement.Source == MoneyMovementSource.BankStatementImport &&
            movement.OccurredAt == occurredAt && movement.Amount == amount &&
            movement.Type == type && movement.ImportComment == importComment;
    }
}
