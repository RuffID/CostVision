using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class AccountRepository(
        ICreateItemRepository<Account, AppDbContextBase> createRepository,
        IGetItemByIdRepository<Account, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<Account, AppDbContextBase> getItemByPredicateRepository,
        IQueryRepository<Account, AppDbContextBase> queryRepository) : IAccountRepository
    {
        public Task<Account?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Account?> GetItemByPredicateAsync(Expression<Func<Account, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Account>> GetItemsByPredicateAsync(Expression<Func<Account, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Account>, IQueryable<Account>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<Account?> GetByIdWithMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .Include(account => account.Members)
                .FirstOrDefaultAsync(account => account.Id == id, ct);

        public Task<Account?> GetOwnedByIdWithMembersAsync(Guid id, Guid ownerUserId, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .Include(account => account.Members)
                .FirstOrDefaultAsync(account => account.Id == id && account.CreatedByUserId == ownerUserId, ct);

        public Task<List<Account>> GetAccessibleByUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default)
            => queryRepository.Query(true)
                .Include(account => account.CreatedByUser)
                .Where(account => (includeArchived || !account.IsArchived) &&
                                  (account.CreatedByUserId == userId || account.Members.Any(member => member.UserId == userId)))
                .ToListAsync(ct);

        public void Create(Account item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Account> entities) => createRepository.CreateRange(entities);
    }
}
