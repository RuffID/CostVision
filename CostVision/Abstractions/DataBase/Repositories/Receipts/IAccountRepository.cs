using CostVision.DataBase;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountRepository : IGetItemByIdRepository<Account, Guid, ApplicationContext>, IGetItemByPredicateRepository<Account, ApplicationContext>, ICreateItemRepository<Account, ApplicationContext>
    {
    }
}