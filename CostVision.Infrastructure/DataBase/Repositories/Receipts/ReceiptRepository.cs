using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ReceiptRepository(ApplicationContext context) : IReceiptRepository
    {
        public Task<Receipt?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

        public Task<Receipt?> GetItemByPredicateAsync(Expression<Func<Receipt, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<Receipt>> GetItemsByPredicateAsync(Expression<Func<Receipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
        {
            IQueryable<Receipt> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            query = query.Skip(skip);
            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public Task<Receipt?> GetByIdWithAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, query => query.Include(receipt => receipt.Accounts).ThenInclude(link => link.Account).AsSplitQuery())
                .FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

        public Task<Receipt?> GetByIdWithAccountsAndMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, query => query
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery())
                .FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

        public Task<Receipt?> GetByIdWithItemsAndAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, query => query
                    .Include(receipt => receipt.Items)
                        .ThenInclude(item => item.Product)
                    .Include(receipt => receipt.Accounts)
                        .ThenInclude(link => link.Account)
                            .ThenInclude(account => account!.Members)
                    .AsSplitQuery())
                .FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

        public Task<List<Receipt>> GetAccessibleByPeriodAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
            => BuildQuery(true, query => query.Include(receipt => receipt.Accounts).ThenInclude(link => link.Account).ThenInclude(account => account!.Members).AsSplitQuery())
                .Where(receipt => receipt.DateTime >= dateFrom &&
                                  receipt.DateTime <= dateTo &&
                                  (receipt.CreatedByUserId == currentUserId ||
                                   receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                                   receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))))
                .ToListAsync(ct);

        public Task<List<Receipt>> GetWithoutItemsAsync(CancellationToken ct = default)
            => BuildQuery(false, query => query.Include(receipt => receipt.Items))
                .Where(receipt => !receipt.Items.Any())
                .ToListAsync(ct);

        public void Create(Receipt item) => context.Set<Receipt>().Add(item);

        public void CreateRange(IEnumerable<Receipt> entities) => context.Set<Receipt>().AddRange(entities);

        public void Delete(Receipt item) => context.Set<Receipt>().Remove(item);

        public void DeleteRange(IEnumerable<Receipt> items) => context.Set<Receipt>().RemoveRange(items);

        private IQueryable<Receipt> BuildQuery(bool asNoTracking, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include)
        {
            IQueryable<Receipt> query = context.Set<Receipt>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
