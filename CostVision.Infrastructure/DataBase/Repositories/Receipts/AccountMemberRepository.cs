using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Domain.Models.Receipts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class AccountMemberRepository(ApplicationContext context) : IAccountMemberRepository
    {
        public Task<AccountMember?> GetItemByPredicateAsync(Expression<Func<AccountMember, bool>> predicate, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
            => BuildQuery(asNoTracking, include).FirstOrDefaultAsync(predicate, ct);

        public Task<List<AccountMember>> GetItemsByPredicateAsync(Expression<Func<AccountMember, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
        {
            IQueryable<AccountMember> query = BuildQuery(asNoTracking, include);
            if (predicate != null)
                query = query.Where(predicate);

            query = query.Skip(skip);
            if (take.HasValue)
                query = query.Take(take.Value);

            return query.ToListAsync(ct);
        }

        public void Create(AccountMember item) => context.Set<AccountMember>().Add(item);

        public void CreateRange(IEnumerable<AccountMember> entities) => context.Set<AccountMember>().AddRange(entities);

        public void Delete(AccountMember item) => context.Set<AccountMember>().Remove(item);

        public void DeleteRange(IEnumerable<AccountMember> items) => context.Set<AccountMember>().RemoveRange(items);

        private IQueryable<AccountMember> BuildQuery(bool asNoTracking, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include)
        {
            IQueryable<AccountMember> query = context.Set<AccountMember>();
            if (asNoTracking)
                query = query.AsNoTracking();

            if (include != null)
                query = include(query);

            return query;
        }
    }
}
