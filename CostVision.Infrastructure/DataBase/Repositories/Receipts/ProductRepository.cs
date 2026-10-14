using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ProductRepository(
        IContextCreateItemRepository<Product, CostVisionContextKey> createRepository,
        IContextGetItemByIdRepository<Product, Guid, CostVisionContextKey> getItemByIdRepository,
        IContextGetItemByPredicateRepository<Product, CostVisionContextKey> getItemByPredicateRepository,
        IContextQueryRepository<Product, CostVisionContextKey> queryRepository) : IProductRepository
    {
        public Task<Product?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Product?> GetItemByPredicateAsync(Expression<Func<Product, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Product>> GetItemsByPredicateAsync(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<int> CountByPredicateAsync(Expression<Func<Product, bool>>? predicate = null, CancellationToken ct = default)
        {
            IQueryable<Product> query = queryRepository.Query(asNoTracking: true);

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            return query.CountAsync(ct);
        }

        public Task<List<TResult>> GetProjectedItemsAsync<TResult>(Expression<Func<Product, bool>>? predicate,
            Func<IQueryable<Product>, IQueryable<TResult>> query, CancellationToken ct = default)
        {
            return ProjectedReadQuery.ExecuteAsync(queryRepository, predicate, query, ct);
        }

        public void Create(Product item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Product> entities) => createRepository.CreateRange(entities);
    }
}
