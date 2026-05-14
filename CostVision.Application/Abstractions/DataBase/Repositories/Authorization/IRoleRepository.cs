using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Authorization
{
    public interface IRoleRepository :
        ICreateItemRepository<Role, AppDbContextBase>,
        IGetItemByIdRepository<Role, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<Role, AppDbContextBase>
    {
        Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default);
    }
}
