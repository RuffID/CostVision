using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ProductRepository(IGetItemByIdRepository<Product, Guid> getItemById,
        IGetItemByPredicateRepository<Product> getItemByPredicate,
        ICreateItemRepository<Product> create,
        IUpsertItemByIdRepository<Product, Guid> upsertItemById,
        IUpsertItemByPredicateRepository<Product> upsertItemByPredicate) : IProductRepository
    {
        public Task<Product?> GetItemByPredicate(Expression<Func<Product, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<Product>> GetItemsByPredicate(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);

        public Task<Product?> GetItemById(Guid id, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemById(id, asNoTracking, include, ct);

        public Task<List<Product>> GetItemsByPredicateAndSortById(Expression<Func<Product, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Product>, IQueryable<Product>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, include, ct);

        public void Create(Product item) => create.Create(item);

        public Task Upsert(Product item, CancellationToken ct = default) => upsertItemById.Upsert(item, ct);

        public Task Upsert(IEnumerable<Product> items, CancellationToken ct = default) => upsertItemById.Upsert(items, ct);

        public Task Upsert(Product item, Expression<Func<Product, bool>> predicate, CancellationToken ct = default)
            => upsertItemByPredicate.Upsert(item, predicate, ct);

        public Task Upsert(IEnumerable<Product> items, Func<Product, Expression<Func<Product, bool>>> predicateFactory, CancellationToken ct = default)
            => upsertItemByPredicate.Upsert(items, predicateFactory, ct);
    }
}
