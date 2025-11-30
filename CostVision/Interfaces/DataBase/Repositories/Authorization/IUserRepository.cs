using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Authorization;

namespace CostVision.Interfaces.DataBase.Repositories.Authorization
{
    public interface IUserRepository : IGetItemByIdRepository<User, Guid>, IGetItemByPredicateRepository<User>, ICreateItemRepository<User>, IUpsertItemByIdRepository<User, Guid>
    {
    }
}
