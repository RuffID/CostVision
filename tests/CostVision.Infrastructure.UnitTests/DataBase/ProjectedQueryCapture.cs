using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CostVision.Infrastructure.UnitTests.DataBase;

internal static class ProjectedQueryCapture
{
    public static void Configure<TEntity, TRepository>(Mock<TRepository> repository, DbContext context,
        Func<IEnumerable<TEntity>> samples, List<string> sql) where TEntity : class
        where TRepository : class, IProjectedReadRepository<TEntity>
    {
        ConfigureResult<TEntity, TRepository, TEntity>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, Guid>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, int>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, string>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, ReadQueryTotals>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, ProductListItemDto>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, StoreListItemDto>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, ProductStorePurchaseDto>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, DashboardReceiptReadDto>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, DashboardPaymentReadDto>(repository, context, samples, sql);
        ConfigureResult<TEntity, TRepository, MoneyMovementDto>(repository, context, samples, sql);
    }

    private static void ConfigureResult<TEntity, TRepository, TResult>(Mock<TRepository> repository, DbContext context,
        Func<IEnumerable<TEntity>> samples, List<string> sql) where TEntity : class
        where TRepository : class, IProjectedReadRepository<TEntity>
    {
        repository.Setup(item => item.GetProjectedItemsAsync(It.IsAny<Expression<Func<TEntity, bool>>?>(),
                It.IsAny<Func<IQueryable<TEntity>, IQueryable<TResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<TEntity, bool>>? predicate,
                Func<IQueryable<TEntity>, IQueryable<TResult>> projection, CancellationToken ct) =>
            {
                IQueryable<TEntity> query = context.Set<TEntity>().AsNoTracking();
                IQueryable<TEntity> sampleQuery = samples().AsQueryable();
                if (predicate != null)
                {
                    query = query.Where(predicate);
                    sampleQuery = sampleQuery.Where(predicate);
                }

                sql.Add(projection(query).ToQueryString());
                return projection(sampleQuery).ToList();
            });
    }
}
