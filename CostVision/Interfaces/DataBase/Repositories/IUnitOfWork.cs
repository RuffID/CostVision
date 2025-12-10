using CostVision.Interfaces.DataBase.Repositories.Authorization;
using CostVision.Interfaces.DataBase.Repositories.Receipts;

namespace CostVision.Interfaces.DataBase.Repositories
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

        Task SaveAsync(CancellationToken ct = default);
        Task ExecuteInTransaction(Func<Task> action, CancellationToken ct = default);
    }
}