using CostVision.DataBase;
using CostVision.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Authorization
{
    public interface IUserRepository : IGetItemByIdRepository<User, Guid, ApplicationContext>, IGetItemByPredicateRepository<User, ApplicationContext>, ICreateItemRepository<User, ApplicationContext>
    {
    }
}
