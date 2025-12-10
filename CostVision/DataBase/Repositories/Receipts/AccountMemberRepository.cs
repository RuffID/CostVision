using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Interfaces.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class AccountMemberRepository(IGetItemByPredicateRepository<AccountMember> getItemByPredicate,
        ICreateItemRepository<AccountMember> create,
        IDeleteItemRepository<AccountMember> delete) : IAccountMemberRepository
    {
        public Task<AccountMember?> GetItemByPredicate(Expression<Func<AccountMember, bool>> predicate, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicate(predicate, asNoTracking, include, ct);

        public Task<List<AccountMember>> GetItemsByPredicate(Expression<Func<AccountMember, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicate(predicate, skip, take, asNoTracking, include, ct);

        public void Create(AccountMember item) => create.Create(item);

        public void Delete(AccountMember item) => delete.Delete(item);

        public void DeleteRange(IEnumerable<AccountMember> entities) => delete.DeleteRange(entities);
    }
}
