using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Репозитории атомарного сценария; сохранение и отказ принадлежат общему scope.
/// </summary>
public interface IReceiptReconciliationUnitOfWork : IUnitOfWorkScope
{
    IMoneyMovementRepository MoneyMovement { get; }
    IReceiptRepository Receipt { get; }
    IMoneyMovementReceiptRepository MoneyMovementReceipt { get; }
}
