using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class ReceiptRepository(IGetItemByIdRepository<Receipt, Guid, ApplicationContext> getItemById,
        IGetItemByPredicateRepository<Receipt, ApplicationContext> getItemByPredicate,
        ICreateItemRepository<Receipt, ApplicationContext> create,
        IDeleteItemRepository<Receipt, ApplicationContext> delete) : IReceiptRepository
    {
        public Task<Receipt?> GetItemByPredicateAsync(Expression<Func<Receipt, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Receipt>> GetItemsByPredicateAsync(Expression<Func<Receipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<Receipt?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemById.GetItemByIdAsync(id, asNoTracking, include, ct);

        public void Create(Receipt item) => create.Create(item);

        public void CreateRange(IEnumerable<Receipt> entities) => create.CreateRange(entities);

        public void Delete(Receipt item) => delete.Delete(item);

        public void DeleteRange(IEnumerable<Receipt> items) => delete.DeleteRange(items);
    }
}
