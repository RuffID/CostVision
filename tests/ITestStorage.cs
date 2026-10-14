using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Tests;

/// <summary>
/// Только набор mocks для многообластных регрессий. Production DI и сценарии этот тип не получают.
/// </summary>
public interface ITestStorage : IUnitOfWorkScope
{
    IUserRepository User { get; }
    IRoleRepository Role { get; }
    IAccountRepository Account { get; }
    IAccountMemberRepository AccountMember { get; }
    IReceiptRepository Receipt { get; }
    IReceiptAccountRepository ReceiptAccount { get; }
    IReceiptItemRepository ReceiptItem { get; }
    IProductRepository Product { get; }
    IStoreRepository Store { get; }
    IMoneyMovementRepository MoneyMovement { get; }
    IMoneyMovementReceiptRepository MoneyMovementReceipt { get; }
}
