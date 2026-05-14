using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ReceiptRepository(
        ICreateItemRepository<Receipt, AppDbContextBase> createRepository,
        IDeleteItemRepository<Receipt, AppDbContextBase> deleteRepository,
        IGetItemByIdRepository<Receipt, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<Receipt, AppDbContextBase> getItemByPredicateRepository,
        IQueryRepository<Receipt, AppDbContextBase> queryRepository) : IReceiptRepository
    {
        public Task<Receipt?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Receipt?> GetItemByPredicateAsync(Expression<Func<Receipt, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Receipt>> GetItemsByPredicateAsync(Expression<Func<Receipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<Receipt?> GetByIdWithAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .Include(receipt => receipt.Accounts)
                    .ThenInclude(link => link.Account)
                .AsSplitQuery()
                .FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

        public Task<Receipt?> GetByIdWithAccountsAndMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .Include(receipt => receipt.Accounts)
                    .ThenInclude(link => link.Account)
                        .ThenInclude(account => account!.Members)
                .AsSplitQuery()
                .FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

        public Task<Receipt?> GetByIdWithItemsAndAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .Include(receipt => receipt.Items)
                    .ThenInclude(item => item.Product)
                .Include(receipt => receipt.Accounts)
                    .ThenInclude(link => link.Account)
                        .ThenInclude(account => account!.Members)
                .AsSplitQuery()
                .FirstOrDefaultAsync(receipt => receipt.Id == id, ct);

        public Task<List<Receipt>> GetAccessibleByPeriodAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
            => queryRepository.Query(true)
                .Include(receipt => receipt.Accounts)
                    .ThenInclude(link => link.Account)
                        .ThenInclude(account => account!.Members)
                .AsSplitQuery()
                .Where(receipt => receipt.DateTime >= dateFrom &&
                                  receipt.DateTime <= dateTo &&
                                  (receipt.CreatedByUserId == currentUserId ||
                                   receipt.Accounts.Any(link => link.Account!.CreatedByUserId == currentUserId) ||
                                   receipt.Accounts.Any(link => link.Account!.Members.Any(member => member.UserId == currentUserId))))
                .AsSplitQuery()
                .ToListAsync(ct);

        public Task<List<Receipt>> GetWithoutItemsAsync(CancellationToken ct = default)
            => queryRepository.Query()
                .Include(receipt => receipt.Items)
                .Where(receipt => !receipt.Items.Any())
                .ToListAsync(ct);

        public void Create(Receipt item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Receipt> entities) => createRepository.CreateRange(entities);

        public void Delete(Receipt item) => deleteRepository.Delete(item);

        public void DeleteRange(IEnumerable<Receipt> items) => deleteRepository.DeleteRange(items);
    }
}
