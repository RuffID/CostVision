using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class MoneyMovementRemovalUnitOfWork(
    IUnitOfWorkScope scope,
    IMoneyMovementRepository moneyMovement,
    IMoneyMovementReceiptRepository moneyMovementReceipt,
    IAccountMemberRepository accountMember)
    : ScenarioUnitOfWork(scope), IMoneyMovementRemovalUnitOfWork
{
    public IMoneyMovementRepository MoneyMovement { get; } = moneyMovement;
    public IMoneyMovementReceiptRepository MoneyMovementReceipt { get; } = moneyMovementReceipt;
    public IAccountMemberRepository AccountMember { get; } = accountMember;
}
