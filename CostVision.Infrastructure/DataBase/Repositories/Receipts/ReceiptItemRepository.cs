using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ReceiptItemRepository(ApplicationContext context) : IReceiptItemRepository
    {
        public Task<ReceiptItem?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(item => item.Id == id, ct);

        public Task<ReceiptItem?> GetItemByPredicateAsync(Expression<Func<ReceiptItem, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<ReceiptItem>> GetItemsByPredicateAsync(Expression<Func<ReceiptItem, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include = null, CancellationToken ct = default)
        {
            IQueryable<ReceiptItem> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            query = query.Skip(skip);
            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public void Create(ReceiptItem item) => context.Set<ReceiptItem>().Add(item);

        public void CreateRange(IEnumerable<ReceiptItem> entities) => context.Set<ReceiptItem>().AddRange(entities);

        private IQueryable<ReceiptItem> BuildQuery(bool asNoTracking, Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>? include)
        {
            IQueryable<ReceiptItem> query = context.Set<ReceiptItem>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
