using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptAccountRepository :
        ICreateItemRepository<ReceiptAccount>,
        IDeleteItemRepository<ReceiptAccount>,
        IGetItemByPredicateRepository<ReceiptAccount>
    {
    }
}
