using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Authorization
{
    public class UserRepository(ApplicationContext context) : IUserRepository
    {
        public Task<User?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(user => user.Id == id, ct);

        public Task<User?> GetItemByPredicateAsync(Expression<Func<User, bool>> predicate, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<User>> GetItemsByPredicateAsync(Expression<Func<User, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
        {
            IQueryable<User> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            if (skip > 0)
                query = query.Skip(skip);

            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public Task<List<User>> GetListWithRolesAsync(bool includeInactive, CancellationToken ct = default)
            => BuildQuery(true, query => query.Include(user => user.UserRoles))
                .Where(user => includeInactive || user.IsActive)
                .ToListAsync(ct);

        public Task<User?> GetByIdWithRolesAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, query => query.Include(user => user.UserRoles))
                .FirstOrDefaultAsync(user => user.Id == id, ct);

        public Task<User?> GetByNormalizedLoginAsync(string normalizedLogin, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, null)
                .FirstOrDefaultAsync(user => user.Login.ToUpper() == normalizedLogin, ct);

        public Task<User?> GetByLoginWithRolesAsync(string login, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, query => query.Include(user => user.Roles))
                .FirstOrDefaultAsync(user => user.Login == login, ct);

        public void Create(User item) => context.Set<User>().Add(item);

        public void CreateRange(IEnumerable<User> entities) => context.Set<User>().AddRange(entities);

        private IQueryable<User> BuildQuery(bool asNoTracking, Func<IQueryable<User>, IQueryable<User>>? include)
        {
            IQueryable<User> query = context.Set<User>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
