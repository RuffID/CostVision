namespace CostVision.Interfaces.DataBase.Repositories.Base
{
    public interface ICreateItemRepository<TEntity> where TEntity : class
    {
        void Create(TEntity item);
    }
}
