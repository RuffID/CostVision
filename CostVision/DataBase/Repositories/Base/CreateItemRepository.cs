using CostVision.Abstractions.DataBase;
using CostVision.Abstractions.DataBase.Repositories.Base;

namespace CostVision.DataBase.Repositories.Base
{
    public class CreateItemRepository<TEntity>(IAppDbContext _context) : ICreateItemRepository<TEntity> where TEntity : class
    {
        public virtual void Create(TEntity entity)
        {
            _context.Set<TEntity>().Add(entity);
        }
    }
}
