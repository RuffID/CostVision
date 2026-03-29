using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class AccountRepository(IGetItemByIdRepository<Account, Guid, ApplicationContext> getItemById,
        IGetItemByPredicateRepository<Account, ApplicationContext> getItemByPredicate,
        ICreateItemRepository<Account, ApplicationContext> create) : IAccountRepository
    {
        public Task<Account?> GetItemByPredicateAsync(Expression<Func<Account, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Account>> GetItemsByPredicateAsync(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<Account?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemByIdAsync(id, asNoTracking, include, ct);

        public void Create(Account item) => create.Create(item);

        public void CreateRange(IEnumerable<Account> entities) => create.CreateRange(entities);
    }
}
