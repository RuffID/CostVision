using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptItemRepository :
        ICreateItemRepository<ReceiptItem, AppDbContextBase>,
        IGetItemByIdRepository<ReceiptItem, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<ReceiptItem, AppDbContextBase>
    {
    }
}
