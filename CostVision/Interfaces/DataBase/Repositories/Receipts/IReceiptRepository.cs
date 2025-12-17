using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Interfaces.DataBase.Repositories.Receipts
{
    public interface IReceiptRepository : IGetItemByIdRepository<Receipt, Guid>, IGetItemByPredicateRepository<Receipt>, ICreateItemRepository<Receipt>, IUpsertItemByIdRepository<Receipt, Guid>, IDeleteItemRepository<Receipt>
    {
    }
}