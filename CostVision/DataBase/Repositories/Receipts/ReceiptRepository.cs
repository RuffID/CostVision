using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptRepository(IGetItemByIdRepository<Receipt, Guid> getItemById,
        IGetItemByPredicateRepository<Receipt> getItemByPredicate,
        ICreateItemRepository<Receipt> create,
        IDeleteItemRepository<Receipt> delete) : IReceiptRepository
    {
        public Task<Receipt?> GetItemByPredicate(Expression<Func<Receipt, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<Receipt>> GetItemsByPredicate(Expression<Func<Receipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);

        public Task<Receipt?> GetItemById(Guid id, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemById(id, asNoTracking, include, ct);

        public Task<List<Receipt>> GetItemsByPredicateAndSortById(Expression<Func<Receipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemsByPredicateAndSortById(predicate, skip, take, asNoTracking, include, ct);

        public void Create(Receipt item) => create.Create(item);
        
        public void Delete(Receipt item) => delete.Delete(item);

        public void DeleteRange(IEnumerable<Receipt> items) => delete.DeleteRange(items);
    }
}
