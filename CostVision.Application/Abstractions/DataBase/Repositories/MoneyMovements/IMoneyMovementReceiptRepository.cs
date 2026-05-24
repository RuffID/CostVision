using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.MoneyMovements;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements
{
    public interface IMoneyMovementReceiptRepository :
        ICreateItemRepository<MoneyMovementReceipt, AppDbContextBase>,
        IDeleteItemRepository<MoneyMovementReceipt, AppDbContextBase>,
        IGetItemByPredicateRepository<MoneyMovementReceipt, AppDbContextBase>
    {
    }
}
