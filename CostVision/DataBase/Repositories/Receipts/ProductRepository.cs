using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ProductRepository(IGetItemByIdRepository<Product, Guid, ApplicationContext> getItemById,
        IGetItemByPredicateRepository<Product, ApplicationContext> getItemByPredicate,
        ICreateItemRepository<Product, ApplicationContext> create) : IProductRepository
    {
        public Task<Product?> GetItemByPredicateAsync(Expression<Func<Product, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Product>> GetItemsByPredicateAsync(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<Product?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemByIdAsync(id, asNoTracking, include, ct);

        public void Create(Product item) => create.Create(item);

        public void CreateRange(IEnumerable<Product> entities) => create.CreateRange(entities);
    }
}
