using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Authorization;

namespace CostVision.Interfaces.DataBase.Repositories.Authorization
{
    public interface IRoleRepository : IGetItemByIdRepository<Role, Guid>, IGetItemByPredicateRepository<Role>, ICreateItemRepository<Role>
    {
        Task<List<Role>> GetItemsByCollection(IEnumerable<Role> items, bool asNoTracking = false, CancellationToken ct = default);
    }
}
