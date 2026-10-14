using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.MoneyMovements
{
    public class MoneyMovementRepository(
        IContextCreateItemRepository<MoneyMovement, CostVisionContextKey> createRepository,
        IContextDeleteItemRepository<MoneyMovement, CostVisionContextKey> deleteRepository,
        IContextGetItemByIdRepository<MoneyMovement, Guid, CostVisionContextKey> getItemByIdRepository,
        IContextGetItemByPredicateRepository<MoneyMovement, CostVisionContextKey> getItemByPredicateRepository,
        IContextQueryRepository<MoneyMovement, CostVisionContextKey> queryRepository) : IMoneyMovementRepository
    {
        public Task<MoneyMovement?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<MoneyMovement?> GetItemByPredicateAsync(Expression<Func<MoneyMovement, bool>> predicate, bool asNoTracking = false, Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<MoneyMovement>> GetItemsByPredicateAsync(Expression<Func<MoneyMovement, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<List<TResult>> GetProjectedItemsAsync<TResult>(Expression<Func<MoneyMovement, bool>>? predicate,
            Func<IQueryable<MoneyMovement>, IQueryable<TResult>> query, CancellationToken ct = default)
        {
            return ProjectedReadQuery.ExecuteAsync(queryRepository, predicate, query, ct);
        }

        public void Create(MoneyMovement item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<MoneyMovement> entities) => createRepository.CreateRange(entities);

        public void Delete(MoneyMovement item) => deleteRepository.Delete(item);

        public void DeleteRange(IEnumerable<MoneyMovement> entities) => deleteRepository.DeleteRange(entities);
    }
}
