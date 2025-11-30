using CostVision.Interfaces.DataBase;
using CostVision.Interfaces.DataBase.Repositories.Base;

namespace CostVision.DataBase.Repositories.Base
{
    public class DeleteItemRepository<TEntity>(IAppDbContext _context) : IDeleteItemRepository<TEntity> where TEntity : class
    {
        public virtual void Delete(TEntity entity)
        {
            _context.Set<TEntity>().Remove(entity);
        }

        public virtual void DeleteRange(IEnumerable<TEntity> entities)
        {
            _context.Set<TEntity>().RemoveRange(entities);
        }
    }
}