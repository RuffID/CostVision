using CostVision.Domain.Models.Authorization;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Authorization
{
    public interface IRoleRepository
    {
        Task<Role?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default);

        Task<Role?> GetItemByPredicateAsync(Expression<Func<Role, bool>> predicate, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default);

        Task<List<Role>> GetItemsByPredicateAsync(Expression<Func<Role, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<Role>, IQueryable<Role>>? include = null, CancellationToken ct = default);

        Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default);

        void Create(Role item);

        void CreateRange(IEnumerable<Role> entities);
    }
}