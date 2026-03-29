using CostVision.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.DataBase.Repositories.Receipts
{
    public class AccountMemberRepository(IGetItemByPredicateRepository<AccountMember, ApplicationContext> getItemByPredicate,
        ICreateItemRepository<AccountMember, ApplicationContext> create,
        IDeleteItemRepository<AccountMember, ApplicationContext> delete) : IAccountMemberRepository
    {
        public Task<AccountMember?> GetItemByPredicateAsync(Expression<Func<AccountMember, bool>> predicate, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<AccountMember>> GetItemsByPredicateAsync(Expression<Func<AccountMember, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
            => getItemByPredicate.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(AccountMember item) => create.Create(item);

        public void CreateRange(IEnumerable<AccountMember> entities) => create.CreateRange(entities);

        public void Delete(AccountMember item) => delete.Delete(item);

        public void DeleteRange(IEnumerable<AccountMember> entities) => delete.DeleteRange(entities);
    }
}
