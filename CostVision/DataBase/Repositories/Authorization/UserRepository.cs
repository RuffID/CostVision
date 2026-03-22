using CostVision.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Authorization;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Authorization
{
    public class UserRepository(IGetItemByIdRepository<User, Guid> getItemById,
        IGetItemByPredicateRepository<User> getItemByPredicate,
        ICreateItemRepository<User> create) : IUserRepository
    {
        public Task<User?> GetItemByPredicate(Expression<Func<User, bool>> predicate, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<User>> GetItemsByPredicate(Expression<Func<User, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);
                
        public Task<User?> GetItemById(Guid id, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemById(id, asNoTracking, include, ct);

        public Task<List<User>> GetItemsByPredicateAndSortById(Expression<Func<User, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, include, ct);

        public void Create(User item) => create.Create(item);
    }
}
