using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ProductRepository(ApplicationContext context) : IProductRepository
    {
        public Task<Product?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(product => product.Id == id, ct);

        public Task<Product?> GetItemByPredicateAsync(Expression<Func<Product, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<Product>> GetItemsByPredicateAsync(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
        {
            IQueryable<Product> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            query = query.Skip(skip);
            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public void Create(Product item) => context.Set<Product>().Add(item);

        public void CreateRange(IEnumerable<Product> entities) => context.Set<Product>().AddRange(entities);

        private IQueryable<Product> BuildQuery(bool asNoTracking, Func<IQueryable<Product>, IQueryable<Product>>? include)
        {
            IQueryable<Product> query = context.Set<Product>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
