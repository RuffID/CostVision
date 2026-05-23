using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class AccountRepository(
        ICreateItemRepository<Account, AppDbContextBase> createRepository,
        IGetItemByIdRepository<Account, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<Account, AppDbContextBase> getItemByPredicateRepository) : IAccountRepository
    {
        public Task<Account?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Account?> GetItemByPredicateAsync(Expression<Func<Account, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Account>> GetItemsByPredicateAsync(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(Account item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Account> entities) => createRepository.CreateRange(entities);
    }
}
