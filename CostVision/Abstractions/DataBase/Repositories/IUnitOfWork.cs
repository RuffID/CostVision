using CostVision.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Abstractions.DataBase.Repositories
{
    public interface IUnitOfWork
    {
        IUserRepository User { get; }
        IRoleRepository Role { get; }
        IAccountRepository Account { get; }
        IReceiptRepository Receipt { get; }
        IReceiptItemRepository ReceiptItem { get; }
        IProductRepository Product { get; }
        IReceiptAccountRepository ReceiptAccount { get; }
        IAccountMemberRepository AccountMember { get; }

        Task SaveChangesAsync(CancellationToken ct = default);
        Task ExecuteInTransaction(Func<Task> action, CancellationToken ct = default);
    }
}