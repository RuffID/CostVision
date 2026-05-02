using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IProductRepository
    {
        Task<Product?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default);

        Task<Product?> GetItemByPredicateAsync(Expression<Func<Product, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default);

        Task<List<Product>> GetItemsByPredicateAsync(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default);

        void Create(Product item);

        void CreateRange(IEnumerable<Product> entities);
    }
}