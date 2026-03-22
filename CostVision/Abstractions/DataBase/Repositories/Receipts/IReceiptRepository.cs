using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptRepository : IGetItemByIdRepository<Receipt, Guid>, IGetItemByPredicateRepository<Receipt>, ICreateItemRepository<Receipt>, IDeleteItemRepository<Receipt>
    {
    }
}