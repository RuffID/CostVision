using CostVision.Domain.Models.Authorization;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Authorization
{
    public interface IUserRepository
    {
        Task<User?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default);

        Task<User?> GetItemByPredicateAsync(Expression<Func<User, bool>> predicate, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default);

        Task<List<User>> GetItemsByPredicateAsync(Expression<Func<User, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default);

        Task<List<User>> GetListWithRolesAsync(bool includeInactive, CancellationToken ct = default);

        Task<User?> GetByIdWithRolesAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<User?> GetByNormalizedLoginAsync(string normalizedLogin, bool asNoTracking = false, CancellationToken ct = default);

        Task<User?> GetByLoginWithRolesAsync(string login, bool asNoTracking = false, CancellationToken ct = default);

        void Create(User item);

        void CreateRange(IEnumerable<User> entities);
    }
}
