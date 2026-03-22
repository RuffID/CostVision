using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptAccountRepository(IGetItemByPredicateRepository<ReceiptAccount> getItemByPredicate,
        ICreateItemRepository<ReceiptAccount> create) : IReceiptAccountRepository
    {
        public Task<ReceiptAccount?> GetItemByPredicate(Expression<Func<ReceiptAccount, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<ReceiptAccount>> GetItemsByPredicate(Expression<Func<ReceiptAccount, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);

        public void Create(ReceiptAccount item) => create.Create(item);
    }
}
