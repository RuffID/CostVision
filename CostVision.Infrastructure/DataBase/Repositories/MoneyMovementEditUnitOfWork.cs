using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class MoneyMovementEditUnitOfWork(
    IUnitOfWorkScope scope,
    IMoneyMovementRepository moneyMovement,
    IAccountMemberRepository accountMember)
    : ScenarioUnitOfWork(scope), IMoneyMovementEditUnitOfWork
{
    public IMoneyMovementRepository MoneyMovement { get; } = moneyMovement;
    public IAccountMemberRepository AccountMember { get; } = accountMember;
}
