using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Authorization;

namespace CostVision.Abstractions.DataBase.Repositories.Authorization
{
    public interface IUserRepository : IGetItemByIdRepository<User, Guid>, IGetItemByPredicateRepository<User>, ICreateItemRepository<User>
    {
    }
}
