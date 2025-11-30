namespace CostVision.Interfaces.DataBase.Repositories.Base
{
    public interface IQueryRepository<TEntity> where TEntity : class
    {
        IQueryable<TEntity> Query(bool asNoTracking = false);
    }
}
