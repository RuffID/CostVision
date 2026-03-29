using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptItemRepository(IGetItemByIdRepository<ReceiptItem, Guid, ApplicationContext> getItemById,
        IGetItemByPredicateRepository<ReceiptItem, ApplicationContext> getItemByPredicate,
        ICreateItemRepository<ReceiptItem, ApplicationContext> create) : IReceiptItemRepository
    {
        public Task<ReceiptItem?> GetItemByPredicateAsync(Expression<Func<ReceiptItem, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<ReceiptItem>> GetItemsByPredicateAsync(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<ReceiptItem?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemByIdAsync(id, asNoTracking, include, ct);

        public void Create(ReceiptItem item) => create.Create(item);

        public void CreateRange(IEnumerable<ReceiptItem> entities) => create.CreateRange(entities);
    }
}
