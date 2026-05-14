using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class AccountMemberRepository(
        ICreateItemRepository<AccountMember, AppDbContextBase> createRepository,
        IDeleteItemRepository<AccountMember, AppDbContextBase> deleteRepository,
        IGetItemByPredicateRepository<AccountMember, AppDbContextBase> getItemByPredicateRepository) : IAccountMemberRepository
    {
        public Task<AccountMember?> GetItemByPredicateAsync(Expression<Func<AccountMember, bool>> predicate, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<AccountMember>> GetItemsByPredicateAsync(Expression<Func<AccountMember, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<AccountMember>, IQueryable<AccountMember>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(AccountMember item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<AccountMember> entities) => createRepository.CreateRange(entities);

        public void Delete(AccountMember item) => deleteRepository.Delete(item);

        public void DeleteRange(IEnumerable<AccountMember> items) => deleteRepository.DeleteRange(items);
    }
}
