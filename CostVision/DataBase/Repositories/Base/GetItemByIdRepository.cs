using CostVision.Interfaces.DataBase;
using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.Entity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Base
{
    public class GetItemByIdRepository<TEntity, TId>(IAppDbContext _context) : 
        IGetItemByIdRepository<TEntity, TId> where TEntity : class, 
        IEntity<TId> where TId : notnull, 
        IEquatable<TId>, IComparable<TId>
    {
        private const int DefaultTake = 100;
        private const int HardMaxTake = 1000;

        public async Task<TEntity?> GetItemById(TId id, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<TEntity, object>>[] includes)
        {
            IQueryable<TEntity> query = _context.Set<TEntity>();

            if (asNoTracking)
                query = query.AsNoTracking();

            foreach (Expression<Func<TEntity, object>> include in includes)
                query = query.Include(include);

            return await query.FirstOrDefaultAsync(x => x.Id.Equals(id), ct);
        }

        public async Task<List<TEntity>> GetItemsByPredicateAndSortById(Expression<Func<TEntity, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<TEntity, object>>[] includes)
        {
            int effectiveTake = take ?? DefaultTake;
            if (effectiveTake <= 0)
                effectiveTake = DefaultTake;
            if (effectiveTake > HardMaxTake)
                effectiveTake = HardMaxTake;
            if (skip < 0)
                skip = 0;

            IQueryable<TEntity> query = _context.Set<TEntity>();

            if (asNoTracking)
                query = query.AsNoTracking();

            if (predicate != null)
                query = query.Where(predicate);

            foreach (var include in includes)
                query = query.Include(include);

            query = query.OrderBy(e => e.Id)
                .Skip(skip)
                .Take(effectiveTake);

            return await query.ToListAsync(ct);
        }        
    }
}
