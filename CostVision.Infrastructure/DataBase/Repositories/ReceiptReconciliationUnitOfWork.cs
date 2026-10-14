using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class ReceiptReconciliationUnitOfWork(
    IUnitOfWorkScope scope,
    IMoneyMovementRepository moneyMovement,
    IReceiptRepository receipt,
    IMoneyMovementReceiptRepository moneyMovementReceipt)
    : ScenarioUnitOfWork(scope), IReceiptReconciliationUnitOfWork
{
    public IMoneyMovementRepository MoneyMovement { get; } = moneyMovement;
    public IReceiptRepository Receipt { get; } = receipt;
    public IMoneyMovementReceiptRepository MoneyMovementReceipt { get; } = moneyMovementReceipt;
}
