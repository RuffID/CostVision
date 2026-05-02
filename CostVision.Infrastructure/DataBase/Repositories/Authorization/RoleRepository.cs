using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Authorization
{
    public class RoleRepository(ApplicationContext context) : IRoleRepository
    {
        public Task<Role?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(role => role.Id == id, ct);

        public Task<Role?> GetItemByPredicateAsync(Expression<Func<Role, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<Role>> GetItemsByPredicateAsync(Expression<Func<Role, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
        {
            IQueryable<Role> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            query = query.Skip(skip);
            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default)
        {
            List<Role> roleItems = items.ToList();
            if (roleItems.Count == 0)
                return Task.FromResult(new List<Role>());

            List<Guid> ids = roleItems.Select(item => item.Id).ToList();
            List<string> names = roleItems.Select(item => item.Name).ToList();
            return BuildQuery(asNoTracking, null)
                .Where(role => ids.Contains(role.Id) || names.Contains(role.Name))
                .ToListAsync(ct);
        }

        public void Create(Role item) => context.Set<Role>().Add(item);

        public void CreateRange(IEnumerable<Role> entities) => context.Set<Role>().AddRange(entities);

        private IQueryable<Role> BuildQuery(bool asNoTracking, Func<IQueryable<Role>, IQueryable<Role>>? include)
        {
            IQueryable<Role> query = context.Set<Role>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
