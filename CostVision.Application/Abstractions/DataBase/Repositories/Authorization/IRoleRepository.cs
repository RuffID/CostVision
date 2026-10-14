using CostVision.Domain.Models.Authorization;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Authorization
{
    public interface IRoleRepository :
        ICreateItemRepository<Role>,
        IGetItemByIdRepository<Role, Guid>,
        IGetItemByPredicateRepository<Role>
    {
    }
}
