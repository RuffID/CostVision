using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Выполняет заданную вызывающим сценарием проекцию без отслеживания сущностей.
/// </summary>
public interface IProjectedReadRepository<TEntity>
{
    Task<List<TResult>> GetProjectedItemsAsync<TResult>(
        Expression<Func<TEntity, bool>>? predicate,
        Func<IQueryable<TEntity>, IQueryable<TResult>> query,
        CancellationToken ct = default);
}
