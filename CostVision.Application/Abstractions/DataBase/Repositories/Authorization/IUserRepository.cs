using CostVision.Domain.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Authorization
{
    public interface IUserRepository :
        ICreateItemRepository<User>,
        IDeleteItemRepository<User>,
        IGetItemByIdRepository<User, Guid>,
        IGetItemByPredicateRepository<User>
    {
    }
}
