using CostVision.Interfaces.DataBase;
using CostVision.Interfaces.DataBase.Repositories.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Base
{
    public class GetItemByPredicateRepository<TEntity>(IAppDbContext context) : IGetItemByPredicateRepository<TEntity> where TEntity : class
    {
        public Task<TEntity?> GetItemByPredicate(Expression<Func<TEntity, bool>> predicate, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = context.Set<TEntity>();

            if (asNoTracking) 
                query = query.AsNoTracking();

            foreach (var include in includes)
                query = query.Include(include);

            return query.FirstOrDefaultAsync(predicate, ct);
        }

        public Task<List<TEntity>> GetItemsByPredicate(Expression<Func<TEntity, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = context.Set<TEntity>();

            if (asNoTracking) 
                query = query.AsNoTracking();

            if (predicate != null)
                query = query.Where(predicate);

            foreach (var include in includes)
                query = query.Include(include);

            if (skip > 0)
                query = query.Skip(skip);

            if (take.HasValue && take.Value > 0)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }
    }
}
