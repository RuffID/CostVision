using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Interfaces.DataBase.Repositories.Receipts
{
    public interface IAccountRepository : IGetItemByIdRepository<Account, Guid>, IGetItemByPredicateRepository<Account>, ICreateItemRepository<Account>, IUpsertItemByIdRepository<Account, Guid>
    {
    }
}