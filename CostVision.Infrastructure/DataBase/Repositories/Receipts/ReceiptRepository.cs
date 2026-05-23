using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class ReceiptRepository(
        ICreateItemRepository<Receipt, AppDbContextBase> createRepository,
        IDeleteItemRepository<Receipt, AppDbContextBase> deleteRepository,
        IGetItemByIdRepository<Receipt, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<Receipt, AppDbContextBase> getItemByPredicateRepository) : IReceiptRepository
    {
        public Task<Receipt?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Receipt?> GetItemByPredicateAsync(Expression<Func<Receipt, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Receipt>> GetItemsByPredicateAsync(Expression<Func<Receipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Receipt>, IQueryable<Receipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(Receipt item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Receipt> entities) => createRepository.CreateRange(entities);

        public void Delete(Receipt item) => deleteRepository.Delete(item);

        public void DeleteRange(IEnumerable<Receipt> items) => deleteRepository.DeleteRange(items);
    }
}
