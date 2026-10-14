using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class BankStatementImportUnitOfWork(
    IUnitOfWorkScope scope,
    IMoneyMovementRepository moneyMovement,
    IAccountMemberRepository accountMember)
    : ScenarioUnitOfWork(scope), IBankStatementImportUnitOfWork
{
    public IMoneyMovementRepository MoneyMovement { get; } = moneyMovement;
    public IAccountMemberRepository AccountMember { get; } = accountMember;
}
