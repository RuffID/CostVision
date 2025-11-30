using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Products;
using CostVision.Models.Products;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Products
{
    public class ProductRepository(IGetItemByIdRepository<Product, Guid> getItemById,
        IGetItemByPredicateRepository<Product> getItemByPredicate,
        ICreateItemRepository<Product> create,
        IUpsertItemByIdRepository<Product, Guid> upsert) : IProductRepository
    {
        public Task<Product?> GetItemByPredicate(Expression<Func<Product, bool>> predicate, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Product, object>>[] includes)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, ct, includes);

        public Task<List<Product>> GetItemsByPredicate(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Product, object>>[] includes)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, ct, includes);

        public Task<Product?> GetItemById(Guid id, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Product, object>>[] includes)
            => getItemById.GetItemById(id, asNoTracking, ct, includes);

        public Task<List<Product>> GetItemsByPredicateAndSortById(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<Product, object>>[] includes)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, ct, includes);

        public void Create(Product item) => create.Create(item);

        public Task Upsert(Product item, CancellationToken ct = default) => upsert.Upsert(item, ct);

        public Task Upsert(IEnumerable<Product> items, CancellationToken ct = default) => upsert.Upsert(items, ct);
    }
}
