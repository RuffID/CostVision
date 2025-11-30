using CostVision.Interfaces.DataBase;
using CostVision.Interfaces.DataBase.Repositories.Base;
using Microsoft.EntityFrameworkCore;

namespace CostVision.DataBase.Repositories.Base
{
    public class QueryRepository<TEntity>(IAppDbContext context) : IQueryRepository<TEntity> where TEntity : class
    {
        public virtual IQueryable<TEntity> Query(bool asNoTracking = false)
        {
            IQueryable<TEntity> query = context.Set<TEntity>();

            if (asNoTracking)
                query = query.AsNoTracking();

            return query;
        }
    }
}
