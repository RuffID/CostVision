using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ReceiptAccountRepository(
        ICreateItemRepository<ReceiptAccount, AppDbContextBase> createRepository,
        IDeleteItemRepository<ReceiptAccount, AppDbContextBase> deleteRepository,
        IGetItemByPredicateRepository<ReceiptAccount, AppDbContextBase> getItemByPredicateRepository) : IReceiptAccountRepository
    {
        public Task<ReceiptAccount?> GetItemByPredicateAsync(Expression<Func<ReceiptAccount, bool>> predicate, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<ReceiptAccount>> GetItemsByPredicateAsync(Expression<Func<ReceiptAccount, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<ReceiptAccount>, IQueryable<ReceiptAccount>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(ReceiptAccount item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<ReceiptAccount> entities) => createRepository.CreateRange(entities);

        public void Delete(ReceiptAccount item) => deleteRepository.Delete(item);

        public void DeleteRange(IEnumerable<ReceiptAccount> items) => deleteRepository.DeleteRange(items);
    }
}
