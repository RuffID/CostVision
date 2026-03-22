using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountRepository : IGetItemByIdRepository<Account, Guid>, IGetItemByPredicateRepository<Account>, ICreateItemRepository<Account>
    {
    }
}