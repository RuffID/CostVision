using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptAccountRepository :
        ICreateItemRepository<ReceiptAccount, AppDbContextBase>,
        IDeleteItemRepository<ReceiptAccount, AppDbContextBase>,
        IGetItemByPredicateRepository<ReceiptAccount, AppDbContextBase>
    {
    }
}
