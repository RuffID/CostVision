using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountRepository :
        ICreateItemRepository<Account, AppDbContextBase>,
        IGetItemByIdRepository<Account, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<Account, AppDbContextBase>
    {
        Task<Account?> GetByIdWithMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<Account?> GetOwnedByIdWithMembersAsync(Guid id, Guid ownerUserId, bool asNoTracking = false, CancellationToken ct = default);

        Task<List<Account>> GetAccessibleByUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default);
    }
}
