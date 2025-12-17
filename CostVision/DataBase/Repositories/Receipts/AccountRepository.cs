using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class AccountRepository(IGetItemByIdRepository<Account, Guid> getItemById,
        IGetItemByPredicateRepository<Account> getItemByPredicate,
        ICreateItemRepository<Account> create) : IAccountRepository
    {
        public Task<Account?> GetItemByPredicate(Expression<Func<Account, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<Account>> GetItemsByPredicate(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);

        public Task<Account?> GetItemById(Guid id, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemById(id, asNoTracking, include, ct);

        public Task<List<Account>> GetItemsByPredicateAndSortById(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, include, ct);

        public void Create(Account item) => create.Create(item);
    }
}
