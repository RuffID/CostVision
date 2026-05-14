using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Domain.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CostVision.Infrastructure.DataBase.Repositories.Authorization
{
    public class UserRepository(
        ICreateItemRepository<User, AppDbContextBase> createRepository,
        IDeleteItemRepository<User, AppDbContextBase> deleteRepository,
        IGetItemByIdRepository<User, Guid, AppDbContextBase> getItemByIdRepository,
        IGetItemByPredicateRepository<User, AppDbContextBase> getItemByPredicateRepository,
        IQueryRepository<User, AppDbContextBase> queryRepository) : IUserRepository
    {
        public Task<User?> GetItemByIdAsync(Guid id, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => getItemByIdRepository.GetItemByIdAsync(id, asNoTracking, include, ct);

        public Task<User?> GetItemByPredicateAsync(Expression<Func<User, bool>> predicate, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemByPredicateAsync(predicate, asNoTracking, include, ct);

        public Task<List<User>> GetItemsByPredicateAsync(Expression<Func<User, bool>>? predicate = null, int skip = 0, int? take = null, bool asNoTracking = false, Func<IQueryable<User>, IQueryable<User>>? include = null, CancellationToken ct = default)
            => getItemByPredicateRepository.GetItemsByPredicateAsync(predicate, skip, take, asNoTracking, include, ct);

        public Task<List<User>> GetListWithRolesAsync(bool includeInactive, CancellationToken ct = default)
            => queryRepository.Query(true)
                .Include(user => user.UserRoles)
                .Where(user => includeInactive || user.IsActive)
                .ToListAsync(ct);

        public Task<User?> GetByIdWithRolesAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .Include(user => user.UserRoles)
                .FirstOrDefaultAsync(user => user.Id == id, ct);

        public Task<User?> GetByNormalizedLoginAsync(string normalizedLogin, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .FirstOrDefaultAsync(user => user.Login.ToUpper() == normalizedLogin, ct);

        public Task<User?> GetByLoginWithRolesAsync(string login, bool asNoTracking = false, CancellationToken ct = default)
            => queryRepository.Query(asNoTracking)
                .Include(user => user.Roles)
                .FirstOrDefaultAsync(user => user.Login == login, ct);

        public void Create(User item) => createRepository.Create(item);

        public void CreateRange(IEnumerable<User> entities) => createRepository.CreateRange(entities);

        public void Delete(User item) => deleteRepository.Delete(item);

        public void DeleteRange(IEnumerable<User> entities) => deleteRepository.DeleteRange(entities);
    }
}
