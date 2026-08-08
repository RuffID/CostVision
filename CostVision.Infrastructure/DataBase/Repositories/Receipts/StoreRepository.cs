using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Receipts
{
    public class StoreRepository(
        ICreateItemRepository<Store, AppDbContextBase> createRepository,
        IGetItemByIdRepository<Store, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<Store, AppDbContextBase> getItemByPredicateRepository,
        IAppDbContext<AppDbContextBase> context) : IStoreRepository
    {
        public Task<Store?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Store>, IQueryable<Store>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Store?> GetItemByPredicateAsync(Expression<Func<Store, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Store>, IQueryable<Store>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Store>> GetItemsByPredicateAsync(Expression<Func<Store, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Store>, IQueryable<Store>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<int> CountByPredicateAsync(Expression<Func<Store, bool>>? predicate = null, CancellationToken ct = default)
        {
            IQueryable<Store> query = context.Set<Store>();

            if (predicate != null)
                query = query.Where(predicate);

            return query.CountAsync(ct);
        }

        public void Create(Store item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Store> entities) => createRepository.CreateRange(entities);
    }
}
