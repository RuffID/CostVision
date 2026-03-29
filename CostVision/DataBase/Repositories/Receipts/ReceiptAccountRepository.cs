using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptAccountRepository(IGetItemByPredicateRepository<ReceiptAccount, ApplicationContext> getItemByPredicate,
        ICreateItemRepository<ReceiptAccount, ApplicationContext> create,
        IDeleteItemRepository<ReceiptAccount, ApplicationContext> delete) : IReceiptAccountRepository
    {
        public Task<ReceiptAccount?> GetItemByPredicateAsync(Expression<Func<ReceiptAccount, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<ReceiptAccount>> GetItemsByPredicateAsync(Expression<Func<ReceiptAccount, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(ReceiptAccount item) => create.Create(item);

        public void CreateRange(IEnumerable<ReceiptAccount> entities) => create.CreateRange(entities);

        public void Delete(ReceiptAccount item) => delete.Delete(item);

        public void DeleteRange(IEnumerable<ReceiptAccount> items) => delete.DeleteRange(items);
    }
}
