using CostVision.DataBase;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptAccountRepository : IGetItemByPredicateRepository<ReceiptAccount, ApplicationContext>, ICreateItemRepository<ReceiptAccount, ApplicationContext>, IDeleteItemRepository<ReceiptAccount, ApplicationContext>
    {
    }
}