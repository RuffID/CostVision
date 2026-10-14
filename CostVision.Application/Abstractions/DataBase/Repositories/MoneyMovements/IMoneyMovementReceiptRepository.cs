using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements
{
    public interface IMoneyMovementReceiptRepository :
        ICreateItemRepository<MoneyMovementReceipt>,
        IDeleteItemRepository<MoneyMovementReceipt>,
        IGetItemByPredicateRepository<MoneyMovementReceipt>
    {
    }
}
