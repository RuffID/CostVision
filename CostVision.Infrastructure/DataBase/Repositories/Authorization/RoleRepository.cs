using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Domain.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Authorization
{
    public class RoleRepository(
        ICreateItemRepository<Role, AppDbContextBase> createRepository,
        IGetItemByIdRepository<Role, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<Role, AppDbContextBase> getItemByPredicateRepository,
        IQueryRepository<Role, AppDbContextBase> queryRepository) : IRoleRepository
    {
        public Task<Role?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<Role?> GetItemByPredicateAsync(Expression<Func<Role, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<Role>> GetItemsByPredicateAsync(Expression<Func<Role, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default)
        {
            List<Role> roleItems = items.ToList();
            if (roleItems.Count == 0)
                return Task.FromResult(new List<Role>());

            List<Guid> ids = roleItems.Select(item => item.Id).ToList();
            List<string> names = roleItems.Select(item => item.Name).ToList();
            return queryRepository.Query(asNoTracking)
                .Where(role => ids.Contains(role.Id) || names.Contains(role.Name))
                .ToListAsync(ct);
        }

        public void Create(Role item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<Role> entities) => createRepository.CreateRange(entities);
    }
}
