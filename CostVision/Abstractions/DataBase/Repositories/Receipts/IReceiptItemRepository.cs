using CostVision.DataBase;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptItemRepository : IGetItemByIdRepository<ReceiptItem, Guid, ApplicationContext>, IGetItemByPredicateRepository<ReceiptItem, ApplicationContext>, ICreateItemRepository<ReceiptItem, ApplicationContext>
    {
    }
}