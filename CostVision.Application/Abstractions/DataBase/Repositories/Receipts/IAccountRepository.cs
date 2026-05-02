using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountRepository
    {
        Task<Account?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default);

        Task<Account?> GetItemByPredicateAsync(Expression<Func<Account, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default);

        Task<List<Account>> GetItemsByPredicateAsync(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default);

        Task<Account?> GetByIdWithMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<Account?> GetOwnedByIdWithMembersAsync(Guid id, Guid ownerUserId, bool asNoTracking = false, CancellationToken ct = default);

        Task<List<Account>> GetAccessibleByUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default);

        void Create(Account item);

        void CreateRange(IEnumerable<Account> entities);
    }
}
