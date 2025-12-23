using CostVision.Interfaces.DataBase;
using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.DataBase.Repositories.Authorization;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using Microsoft.EntityFrameworkCore.Storage;

namespace CostVision.DataBase.Repositories
{
    public class UnitOfWork(IAppDbContext context,
        IUserRepository user,
        IRoleRepository role,
        IAccountRepository account,
        IReceiptRepository receipt,
        IReceiptItemRepository receiptItem,
        IProductRepository product,
        IReceiptAccountRepository receiptAccount,
        IAccountMemberRepository accountMember) : IUnitOfWork
    {
        public IUserRepository User { get; } = user;
        public IRoleRepository Role { get; } = role;
        public IAccountRepository Account { get; set; } = account;
        public IReceiptRepository Receipt { get; set; } = receipt;
        public IReceiptItemRepository ReceiptItem { get; set; } = receiptItem;
        public IProductRepository Product { get; set; } = product;
        public IReceiptAccountRepository ReceiptAccount { get; set; } = receiptAccount;
        public IAccountMemberRepository AccountMember { get; set; } = accountMember;

        public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChanges(ct);

        public async Task ExecuteInTransaction(Func<Task> action, CancellationToken ct = default)
        {
            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync(ct);

            try
            {
                await action();
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
