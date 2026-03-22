using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptItemRepository : IGetItemByIdRepository<ReceiptItem, Guid>, IGetItemByPredicateRepository<ReceiptItem>, ICreateItemRepository<ReceiptItem>
    {
    }
}