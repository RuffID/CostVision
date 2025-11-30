namespace CostVision.Interfaces.DataBase.Repositories.Base
{
    public interface IDeleteItemRepository<TEntity> where TEntity : class
    {
        void Delete(TEntity item);
        void DeleteRange(IEnumerable<TEntity> entities);
    }
}
