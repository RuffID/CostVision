using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements
{
    public interface IMoneyMovementRepository :
        ICreateItemRepository<MoneyMovement, AppDbContextBase>,
        IDeleteItemRepository<MoneyMovement, AppDbContextBase>,
        IGetItemByIdRepository<MoneyMovement, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<MoneyMovement, AppDbContextBase>
    {
    }
}
