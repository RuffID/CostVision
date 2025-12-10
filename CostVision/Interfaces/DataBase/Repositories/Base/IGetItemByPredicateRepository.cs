using System.Linq.Expressions;

namespace CostVision.Interfaces.DataBase.Repositories.Base
{
    public interface IGetItemByPredicateRepository<TEntity> where TEntity : class
    {
        Task<TEntity?> GetItemByPredicate(Expression<Func<TEntity, bool>> predicate, bool asNoTracking = false, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default);

        Task<List<TEntity>> GetItemsByPredicate(Expression<Func<TEntity, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default);
    }
}