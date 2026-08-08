﻿using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Application.Abstractions.DataBase.Repositories
{
    public interface IUnitOfWork
    {
        IUserRepository User { get; }
        IRoleRepository Role { get; }
        IAccountRepository Account { get; }
        IReceiptRepository Receipt { get; }
        IReceiptItemRepository ReceiptItem { get; }
        IStoreRepository Store { get; }
        IProductRepository Product { get; }
        IReceiptAccountRepository ReceiptAccount { get; }
        IAccountMemberRepository AccountMember { get; }
        IMoneyMovementRepository MoneyMovement { get; }
        IMoneyMovementReceiptRepository MoneyMovementReceipt { get; }

        Task SaveChangesAsync(CancellationToken ct = default);
        Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken ct = default);
    }
}

