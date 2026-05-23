using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IAccountRepository :
        ICreateItemRepository<Account, AppDbContextBase>,
        IGetItemByIdRepository<Account, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<Account, AppDbContextBase>
    {
    }
}
