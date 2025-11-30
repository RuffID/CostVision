using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Interfaces.DataBase.Repositories.Receipts
{
    public interface IReceiptItemRepository : IGetItemByIdRepository<ReceiptItem, Guid>, IGetItemByPredicateRepository<ReceiptItem>, ICreateItemRepository<ReceiptItem>, IUpsertItemByIdRepository<ReceiptItem, Guid>
    {
    }
}