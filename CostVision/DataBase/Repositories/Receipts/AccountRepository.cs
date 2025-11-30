using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class AccountRepository(IGetItemByIdRepository<Account, Guid> getItemById,
        IGetItemByPredicateRepository<Account> getItemByPredicate,
        ICreateItemRepository<Account> create,
        IUpsertItemByIdRepository<Account, Guid> upsert) : IAccountRepository
    {
        public Task<Account?> GetItemByPredicate(Expression<Func<Account, bool>> predicate, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Account, object>>[] includes)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, ct, includes);

        public Task<List<Account>> GetItemsByPredicate(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Account, object>>[] includes)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, ct, includes);

        public Task<Account?> GetItemById(Guid id, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Account, object>>[] includes)
            => getItemById.GetItemById(id, asNoTracking, ct, includes);

        public Task<List<Account>> GetItemsByPredicateAndSortById(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Account, object>>[] includes)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, ct, includes);

        public void Create(Account item) => create.Create(item);

        public Task Upsert(Account item, CancellationToken ct = default) => upsert.Upsert(item, ct);

        public Task Upsert(IEnumerable<Account> items, CancellationToken ct = default) => upsert.Upsert(items, ct);
    }
}
