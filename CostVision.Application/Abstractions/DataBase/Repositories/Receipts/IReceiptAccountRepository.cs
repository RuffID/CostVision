using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptAccountRepository
    {
        Task<ReceiptAccount?> GetItemByPredicateAsync(Expression<Func<ReceiptAccount, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default);

        Task<List<ReceiptAccount>> GetItemsByPredicateAsync(Expression<Func<ReceiptAccount, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default);

        void Create(ReceiptAccount item);

        void CreateRange(IEnumerable<ReceiptAccount> entities);

        void Delete(ReceiptAccount item);

        void DeleteRange(IEnumerable<ReceiptAccount> items);
    }
}