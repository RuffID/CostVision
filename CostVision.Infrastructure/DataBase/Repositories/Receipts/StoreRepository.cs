using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class StoreRepository(
        IContextCreateItemRepository<Store, CostVisionContextKey> createRepository,
        IContextGetItemByIdRepository<Store, Guid, CostVisionContextKey> getItemByIdRepository,
        IContextGetItemByPredicateRepository<Store, CostVisionContextKey> getItemByPredicateRepository,
        IContextQueryRepository<Store, CostVisionContextKey> queryRepository) : IStoreRepository
    {
        public Task<Store?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Store>, IQueryable<Store>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Store?> GetItemByPredicateAsync(Expression<Func<Store, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Store>, IQueryable<Store>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Store>> GetItemsByPredicateAsync(Expression<Func<Store, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Store>, IQueryable<Store>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<int> CountByPredicateAsync(Expression<Func<Store, bool>>? predicate = null, CancellationToken ct = default)
        {
            IQueryable<Store> query = queryRepository.Query(asNoTracking: true);

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            return query.CountAsync(ct);
        }

        public Task<List<TResult>> GetProjectedItemsAsync<TResult>(Expression<Func<Store, bool>>? predicate,
            Func<IQueryable<Store>, IQueryable<TResult>> query, CancellationToken ct = default)
        {
            return ProjectedReadQuery.ExecuteAsync(queryRepository, predicate, query, ct);
        }

        public void Create(Store item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Store> entities) => createRepository.CreateRange(entities);
    }
}
