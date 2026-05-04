using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptRepository
    {
        Task<Receipt?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default);

        Task<Receipt?> GetItemByPredicateAsync(Expression<Func<Receipt, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default);

        Task<List<Receipt>> GetItemsByPredicateAsync(Expression<Func<Receipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default);

        Task<Receipt?> GetByIdWithAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<Receipt?> GetByIdWithAccountsAndMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<Receipt?> GetByIdWithItemsAndAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<List<Receipt>> GetAccessibleByPeriodAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default);

        Task<List<Receipt>> GetWithoutItemsAsync(CancellationToken ct = default);

        void Create(Receipt item);

        void CreateRange(IEnumerable<Receipt> entities);

        void Delete(Receipt item);

        void DeleteRange(IEnumerable<Receipt> items);
    }
}
