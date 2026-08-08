using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using EFCoreLibrary.Abstractions.Database;
using Microsoft.EntityFrameworkCore.Storage;

namespace CostVision.Infrastructure.DataBase.Repositories
{
    public class UnitOfWork(IAppDbContext<ApplicationContext> context,
        IUserRepository user,
        IRoleRepository role,
        IAccountRepository account,
        IReceiptRepository receipt,
        IReceiptItemRepository receiptItem,
        IStoreRepository store,
        IProductRepository product,
        IReceiptAccountRepository receiptAccount,
        IAccountMemberRepository accountMember,
        IMoneyMovementRepository moneyMovement,
        IMoneyMovementReceiptRepository moneyMovementReceipt) : IUnitOfWork
    {
        public IUserRepository User { get; } = user;
        public IRoleRepository Role { get; } = role;
        public IAccountRepository Account { get; } = account;
        public IReceiptRepository Receipt { get; } = receipt;
        public IReceiptItemRepository ReceiptItem { get; } = receiptItem;
        public IStoreRepository Store { get; } = store;
        public IProductRepository Product { get; } = product;
        public IReceiptAccountRepository ReceiptAccount { get; } = receiptAccount;
        public IAccountMemberRepository AccountMember { get; } = accountMember;
        public IMoneyMovementRepository MoneyMovement { get; } = moneyMovement;
        public IMoneyMovementReceiptRepository MoneyMovementReceipt { get; } = moneyMovementReceipt;

        public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChanges(ct);

        public async Task ExecuteInTransaction(Func<CancellationToken, Task> action, CancellationToken ct = default)
        {
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

            try
            {
                await action(ct);
                await SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}
