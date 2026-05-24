using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.MoneyMovements
{
    public class MoneyMovementReceiptRepository(
        ICreateItemRepository<MoneyMovementReceipt, AppDbContextBase> createRepository,
        IDeleteItemRepository<MoneyMovementReceipt, AppDbContextBase> deleteRepository,
        IGetItemByPredicateRepository<MoneyMovementReceipt, AppDbContextBase> getItemByPredicateRepository) : IMoneyMovementReceiptRepository
    {
        public Task<MoneyMovementReceipt?> GetItemByPredicateAsync(Expression<Func<MoneyMovementReceipt, bool>> predicate, bool asNoTracking = false, Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<MoneyMovementReceipt>> GetItemsByPredicateAsync(Expression<Func<MoneyMovementReceipt, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public void Create(MoneyMovementReceipt item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<MoneyMovementReceipt> entities) => createRepository.CreateRange(entities);

        public void Delete(MoneyMovementReceipt item) => deleteRepository.Delete(item);

        public void DeleteRange(IEnumerable<MoneyMovementReceipt> items) => deleteRepository.DeleteRange(items);
    }
}
