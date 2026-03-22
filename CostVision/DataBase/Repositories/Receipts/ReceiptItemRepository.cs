using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptItemRepository(IGetItemByIdRepository<ReceiptItem, Guid> getItemById,
        IGetItemByPredicateRepository<ReceiptItem> getItemByPredicate,
        ICreateItemRepository<ReceiptItem> create) : IReceiptItemRepository
    {
        public Task<ReceiptItem?> GetItemByPredicate(Expression<Func<ReceiptItem, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<ReceiptItem>> GetItemsByPredicate(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);

        public Task<ReceiptItem?> GetItemById(Guid id, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemById(id, asNoTracking, include, ct);

        public Task<List<ReceiptItem>> GetItemsByPredicateAndSortById(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, include, ct);

        public void Create(ReceiptItem item) => create.Create(item);
    }
}
