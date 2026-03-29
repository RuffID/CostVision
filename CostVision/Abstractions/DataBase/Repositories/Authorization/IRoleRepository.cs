using CostVision.DataBase;
using CostVision.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Authorization
{
    public interface IRoleRepository : IGetItemByIdRepository<Role, Guid, ApplicationContext>, IGetItemByPredicateRepository<Role, ApplicationContext>, ICreateItemRepository<Role, ApplicationContext>
    {
        Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default);
    }
}
