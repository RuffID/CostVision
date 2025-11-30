using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptItemRepository(IGetItemByIdRepository<ReceiptItem, Guid> getItemById,
        IGetItemByPredicateRepository<ReceiptItem> getItemByPredicate,
        ICreateItemRepository<ReceiptItem> create,
        IUpsertItemByIdRepository<ReceiptItem, Guid> upsert) : IReceiptItemRepository
    {
        public Task<ReceiptItem?> GetItemByPredicate(Expression<Func<ReceiptItem, bool>> predicate, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<ReceiptItem, object>>[] includes)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, ct, includes);

        public Task<List<ReceiptItem>> GetItemsByPredicate(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<ReceiptItem, object>>[] includes)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, ct, includes);

        public Task<ReceiptItem?> GetItemById(Guid id, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<ReceiptItem, object>>[] includes)
            => getItemById.GetItemById(id, asNoTracking, ct, includes);

        public Task<List<ReceiptItem>> GetItemsByPredicateAndSortById(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<ReceiptItem, object>>[] includes)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, ct, includes);

        public void Create(ReceiptItem item) => create.Create(item);

        public Task Upsert(ReceiptItem item, CancellationToken ct = default) => upsert.Upsert(item, ct);

        public Task Upsert(IEnumerable<ReceiptItem> items, CancellationToken ct = default) => upsert.Upsert(items, ct);
    }
}
