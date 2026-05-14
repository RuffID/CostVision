using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ReceiptAccountRepository(ApplicationContext context) : IReceiptAccountRepository
    {
        public Task<ReceiptAccount?> GetItemByPredicateAsync(Expression<Func<ReceiptAccount, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<ReceiptAccount>> GetItemsByPredicateAsync(Expression<Func<ReceiptAccount, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
        {
            IQueryable<ReceiptAccount> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            if (skip > 0)
                query = query.Skip(skip);

            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public void Create(ReceiptAccount item) => context.Set<ReceiptAccount>().Add(item);

        public void CreateRange(IEnumerable<ReceiptAccount> entities) => context.Set<ReceiptAccount>().AddRange(entities);

        public void Delete(ReceiptAccount item) => context.Set<ReceiptAccount>().Remove(item);

        public void DeleteRange(IEnumerable<ReceiptAccount> items) => context.Set<ReceiptAccount>().RemoveRange(items);

        private IQueryable<ReceiptAccount> BuildQuery(bool asNoTracking, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include)
        {
            IQueryable<ReceiptAccount> query = context.Set<ReceiptAccount>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
