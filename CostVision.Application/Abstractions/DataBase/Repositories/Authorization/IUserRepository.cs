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
    }
}
