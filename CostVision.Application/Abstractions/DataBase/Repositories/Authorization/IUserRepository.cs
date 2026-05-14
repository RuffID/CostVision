using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Authorization
{
    public interface IUserRepository :
        ICreateItemRepository<User, AppDbContextBase>,
        IDeleteItemRepository<User, AppDbContextBase>,
        IGetItemByIdRepository<User, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<User, AppDbContextBase>
    {
        Task<List<User>> GetListWithRolesAsync(bool includeInactive, CancellationToken ct = default);

        Task<User?> GetByIdWithRolesAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<User?> GetByNormalizedLoginAsync(string normalizedLogin, bool asNoTracking = false, CancellationToken ct = default);

        Task<User?> GetByLoginWithRolesAsync(string login, bool asNoTracking = false, CancellationToken ct = default);
    }
}
