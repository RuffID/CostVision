using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ReceiptItemRepository(
        ICreateItemRepository<ReceiptItem, AppDbContextBase> createRepository,
        IGetItemByIdRepository<ReceiptItem, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<ReceiptItem, AppDbContextBase> getItemByPredicateRepository) : IReceiptItemRepository
    {
        public Task<ReceiptItem?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<ReceiptItem?> GetItemByPredicateAsync(Expression<Func<ReceiptItem, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<ReceiptItem>> GetItemsByPredicateAsync(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(ReceiptItem item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<ReceiptItem> entities) => createRepository.CreateRange(entities);
    }
}
