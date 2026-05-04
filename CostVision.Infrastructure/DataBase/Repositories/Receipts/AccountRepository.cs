using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class AccountRepository(ApplicationContext context) : IAccountRepository
    {
        public Task<Account?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(account => account.Id == id, ct);

        public Task<Account?> GetItemByPredicateAsync(Expression<Func<Account, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<Account>> GetItemsByPredicateAsync(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
        {
            IQueryable<Account> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            query = query.Skip(skip);
            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public Task<Account?> GetByIdWithMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, query => query.Include(account => account.Members))
                .FirstOrDefaultAsync(account => account.Id == id, ct);

        public Task<Account?> GetOwnedByIdWithMembersAsync(Guid id, Guid ownerUserId, bool asNoTracking = false, CancellationToken ct = default)
            => BuildQuery(asNoTracking, query => query.Include(account => account.Members))
                .FirstOrDefaultAsync(account => account.Id == id && account.CreatedByUserId == ownerUserId, ct);

        public Task<List<Account>> GetAccessibleByUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default)
            => BuildQuery(true, query => query.Include(account => account.CreatedByUser))
                .Where(account => (includeArchived || !account.IsArchived) &&
                                  (account.CreatedByUserId == userId || account.Members.Any(member => member.UserId == userId)))
                .ToListAsync(ct);

        public void Create(Account item) => context.Set<Account>().Add(item);

        public void CreateRange(IEnumerable<Account> entities) => context.Set<Account>().AddRange(entities);

        private IQueryable<Account> BuildQuery(bool asNoTracking, Func<IQueryable<Account>, IQueryable<Account>>? include)
        {
            IQueryable<Account> query = context.Set<Account>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
