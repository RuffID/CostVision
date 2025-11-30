using CostVision.Interfaces.DataBase.Repositories.Authorization;
using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Authorization;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Authorization
{
    public class UserRepository(IGetItemByIdRepository<User, Guid> getItemById,
        IGetItemByPredicateRepository<User> getItemByPredicate,
        ICreateItemRepository<User> create,
        IUpsertItemByIdRepository<User, Guid> upsert) : IUserRepository
    {
        public Task<User?> GetItemByPredicate(Expression<Func<User, bool>> predicate, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<User, object>>[] includes)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, ct, includes);

        public Task<List<User>> GetItemsByPredicate(Expression<Func<User, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<User, object>>[] includes)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, ct, includes);

        public Task<User?> GetItemById(Guid id, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<User, object>>[] includes)
            => getItemById.GetItemById(id, asNoTracking, ct, includes);

        public Task<List<User>> GetItemsByPredicateAndSortById(Expression<Func<User, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<User, object>>[] includes)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, ct, includes);

        public void Create(User item) => create.Create(item);

        public Task Upsert(User item, CancellationToken ct = default) => upsert.Upsert(item, ct);

        public Task Upsert(IEnumerable<User> items, CancellationToken ct = default) => upsert.Upsert(items, ct);
    }
}
