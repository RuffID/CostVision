using CostVision.Domain.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountMemberRepository
    {
        Task<AccountMember?> GetItemByPredicateAsync(Expression<Func<AccountMember, bool>> predicate, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default);

        Task<List<AccountMember>> GetItemsByPredicateAsync(Expression<Func<AccountMember, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default);

        void Create(AccountMember item);

        void CreateRange(IEnumerable<AccountMember> entities);

        void Delete(AccountMember item);

        void DeleteRange(IEnumerable<AccountMember> items);
    }
}