using CostVision.DataBase;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptRepository : IGetItemByIdRepository<Receipt, Guid, ApplicationContext>, IGetItemByPredicateRepository<Receipt, ApplicationContext>, ICreateItemRepository<Receipt, ApplicationContext>, IDeleteItemRepository<Receipt, ApplicationContext>
    {
    }
}