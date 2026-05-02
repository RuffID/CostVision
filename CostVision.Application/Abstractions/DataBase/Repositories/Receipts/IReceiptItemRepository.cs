using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptItemRepository
    {
        Task<ReceiptItem?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default);

        Task<ReceiptItem?> GetItemByPredicateAsync(Expression<Func<ReceiptItem, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default);

        Task<List<ReceiptItem>> GetItemsByPredicateAsync(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default);

        void Create(ReceiptItem item);

        void CreateRange(IEnumerable<ReceiptItem> entities);
    }
}