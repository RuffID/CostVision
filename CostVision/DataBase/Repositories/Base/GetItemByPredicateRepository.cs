using CostVision.Abstractions.DataBase;
using CostVision.Abstractions.DataBase.Repositories.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Base
{
    public class GetItemByPredicateRepository<TEntity>(IAppDbContext context) : IGetItemByPredicateRepository<TEntity> where TEntity : class
    {
        public Task<TEntity?> GetItemByPredicate(Expression<Func<TEntity, bool>> predicate, bool asNoTracking = false, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default)
        {
            IQueryable<TEntity> query = context.Set<TEntity>();

            if (asNoTracking) 
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query.FirstOrDefaultAsync(predicate, ct);
        }

        public Task<List<TEntity>> GetItemsByPredicate(Expression<Func<TEntity, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default)
        {
            IQueryable<TEntity> query = context.Set<TEntity>();

            if (asNoTracking) 
                query = query.AsNoTracking();

            if (predicate != null)
                query = query.Where(predicate);

            if (include != null)
                query = include(query);

            if (skip > 0)
                query = query.Skip(skip);

            if (take.HasValue && take.Value > 0)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }
    }
}
