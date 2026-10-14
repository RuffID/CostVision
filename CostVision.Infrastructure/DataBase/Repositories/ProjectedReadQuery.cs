using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;

namespace CostVision.Infrastructure.DataBase.Repositories;

internal static class ProjectedReadQuery
{
    public static Task<List<TResult>> ExecuteAsync<TEntity, TResult>(
        IContextQueryRepository<TEntity, CostVisionContextKey> repository,
        Expression<Func<TEntity, bool>>? predicate,
        Func<IQueryable<TEntity>, IQueryable<TResult>> projection,
        CancellationToken ct) where TEntity : class
    {
        IQueryable<TEntity> query = repository.Query(asNoTracking: true);
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return projection(query).ToListAsync(ct);
    }
}
