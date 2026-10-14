using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements
{
    public interface IMoneyMovementRepository :
        ICreateItemRepository<MoneyMovement>,
        IDeleteItemRepository<MoneyMovement>,
        IGetItemByIdRepository<MoneyMovement, Guid>,
        IGetItemByPredicateRepository<MoneyMovement>,
        IProjectedReadRepository<MoneyMovement>
    {
    }
}
