using CostVision.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Authorization
{
    public class RoleRepository(IGetItemByIdRepository<Role, Guid, ApplicationContext> getItemById,
        IGetItemByPredicateRepository<Role, ApplicationContext> getByPredicate,
        ICreateItemRepository<Role, ApplicationContext> create) : IRoleRepository
    {
        public Task<Role?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default)
        {
            if (!items.Any())
                return Task.FromResult(new List<Role>());

            List<Guid> ids = items.Select(i => i.Id).ToList();
            List<string> names = items.Select(i => i.Name).ToList();

            return getByPredicate.GetItemsByPredicateAsync(predicate: r => ids.Contains(r.Id) || names.Contains(r.Name), asNoTracking: asNoTracking, ct: ct);
        }

        public Task<Role?> GetItemByPredicateAsync(Expression<Func<Role, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getByPredicate.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Role>> GetItemsByPredicateAsync(Expression<Func<Role, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getByPredicate.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(Role item) => create.Create(item);

        public void CreateRange(IEnumerable<Role> entities) => create.CreateRange(entities);
    }
}
