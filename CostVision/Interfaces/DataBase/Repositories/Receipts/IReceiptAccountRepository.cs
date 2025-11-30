using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Interfaces.DataBase.Repositories.Receipts
{
    public interface IReceiptAccountRepository : IGetItemByPredicateRepository<ReceiptAccount>, ICreateItemRepository<ReceiptAccount>
    {
    }
}