using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class ReceiptLinkUnitOfWork(
    IUnitOfWorkScope scope,
    IMoneyMovementRepository moneyMovement,
    IReceiptRepository receipt,
    IMoneyMovementReceiptRepository moneyMovementReceipt,
    IAccountMemberRepository accountMember)
    : ScenarioUnitOfWork(scope), IReceiptLinkUnitOfWork
{
    public IMoneyMovementRepository MoneyMovement { get; } = moneyMovement;
    public IReceiptRepository Receipt { get; } = receipt;
    public IMoneyMovementReceiptRepository MoneyMovementReceipt { get; } = moneyMovementReceipt;
    public IAccountMemberRepository AccountMember { get; } = accountMember;
}
