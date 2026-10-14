using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Репозитории атомарного сценария; сохранение и отказ принадлежат общему scope.
/// </summary>
public interface IMoneyMovementRemovalUnitOfWork : IUnitOfWorkScope
{
    IMoneyMovementRepository MoneyMovement { get; }
    IMoneyMovementReceiptRepository MoneyMovementReceipt { get; }
    IAccountMemberRepository AccountMember { get; }
}
