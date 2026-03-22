using CostVision.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Authorization;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Authorization
{
    public class RoleRepository(IGetItemByIdRepository<Role, Guid> getItemById,
        IGetItemByPredicateRepository<Role> getByPredicate, 
        ICreateItemRepository<Role> create) : IRoleRepository
    {
        public Task<List<Role>> GetItemsByPredicateAndSortById(Expression<Func<Role, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, include, ct);

        public Task<Role?> GetItemById(Guid id, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemById(id, asNoTracking, include, ct);

        public Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default)
        {
            if (items == null || !items.Any())
                return Task.FromResult(new List<Role>());

            List<Guid> ids = items.Select(i => i.Id).ToList();
            List<string> names = items.Select(i => i.Name).ToList();

            return getByPredicate.GetItemsByPredicate(predicate: r => ids.Contains(r.Id) || names.Contains(r.Name), asNoTracking: asNoTracking, ct: ct);
        }

        public Task<Role?> GetItemByPredicate(Expression<Func<Role, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<Role>> GetItemsByPredicate(Expression<Func<Role, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null,  CancellationToken ct = default)
            => getByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);

        public void Create(Role item) => create.Create(item);

    }
}
