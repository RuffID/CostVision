using CostVision.Interfaces.DataBase;
using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.Entity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Base
{
    public sealed class UpsertItemByPredicateRepository<TEntity>(IAppDbContext context) : IUpsertItemByPredicateRepository<TEntity>
        where TEntity : class, ICopyable<TEntity>
    {
        public async Task Upsert(TEntity item, Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        {
            TEntity? existing = await context.Set<TEntity>().FirstOrDefaultAsync(predicate, ct);

            if (existing is null)
            {
                context.Set<TEntity>().Add(item);
                return;
            }

            existing.CopyData(item);
        }

        public async Task Upsert(IEnumerable<TEntity> items, Func<TEntity, Expression<Func<TEntity, bool>>> predicateFactory, CancellationToken ct = default)
        {
            foreach (TEntity item in items)
            {
                Expression<Func<TEntity, bool>> predicate = predicateFactory(item);

                TEntity? existing = await context.Set<TEntity>().FirstOrDefaultAsync(predicate, ct);

                if (existing is null)
                    context.Set<TEntity>().Add(item);
                else
                    existing.CopyData(item);
            }
        }
    }
}
