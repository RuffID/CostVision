using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Dtos.MoneyMovements;
using Moq;

namespace CostVision.Application.UnitTests;

internal static class ProjectedReadTestFactory
{
    public static void Configure<TEntity, TRepository>(Mock<TRepository> repository, Func<IEnumerable<TEntity>> source,
        Action<IQueryable<TEntity>>? observeRoots = null)
        where TRepository : class, IProjectedReadRepository<TEntity>
    {
        ConfigureResult<TEntity, TRepository, TEntity>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, Guid>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, int>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, string>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, ReadQueryTotals>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, ProductListItemDto>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, StoreListItemDto>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, ProductStorePurchaseDto>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, DashboardReceiptReadDto>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, DashboardPaymentReadDto>(repository, source, observeRoots);
        ConfigureResult<TEntity, TRepository, MoneyMovementDto>(repository, source, observeRoots);
    }

    private static void ConfigureResult<TEntity, TRepository, TResult>(Mock<TRepository> repository, Func<IEnumerable<TEntity>> source,
        Action<IQueryable<TEntity>>? observeRoots)
        where TRepository : class, IProjectedReadRepository<TEntity>
    {
        repository.Setup(item => item.GetProjectedItemsAsync(
                It.IsAny<Expression<Func<TEntity, bool>>?>(),
                It.IsAny<Func<IQueryable<TEntity>, IQueryable<TResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<TEntity, bool>>? predicate,
                Func<IQueryable<TEntity>, IQueryable<TResult>> projection, CancellationToken ct) =>
            {
                IQueryable<TEntity> query = source().AsQueryable();
                if (predicate != null)
                {
                    query = query.Where(predicate);
                }

                observeRoots?.Invoke(query);

                // Исполняется actual query-композиция. Это модель LINQ, не SQL или EF-материализация.
                return projection(query).ToList();
            });
    }
}
