using CostVision.Interfaces.Entity;
using System.Linq.Expressions;

namespace CostVision.Interfaces.DataBase.Repositories.Base
{
    public interface IGetItemByIdRepository<TEntity, TId> where TEntity : class, IEntity<TId> where TId : notnull, IEquatable<TId>, IComparable<TId>
    {
        Task<TEntity?> GetItemById(TId id, bool asNoTracking = false, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default);
        Task<List<TEntity>> GetItemsByPredicateAndSortById(Expression<Func<TEntity, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null, CancellationToken ct = default);
    }
}