using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptAccountRepository(IGetItemByPredicateRepository<ReceiptAccount> getItemByPredicate,
        ICreateItemRepository<ReceiptAccount> create) : IReceiptAccountRepository
    {
        public Task<ReceiptAccount?> GetItemByPredicate(Expression<Func<ReceiptAccount, bool>> predicate, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<ReceiptAccount, object>>[] includes)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, ct, includes);

        public Task<List<ReceiptAccount>> GetItemsByPredicate(Expression<Func<ReceiptAccount, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, CancellationToken ct = default, params Expression<Func<ReceiptAccount, object>>[] includes)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, ct, includes);

        public void Create(ReceiptAccount item) => create.Create(item);
    }
}
