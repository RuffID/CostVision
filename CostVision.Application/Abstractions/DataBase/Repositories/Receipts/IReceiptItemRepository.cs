using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptItemRepository :
        ICreateItemRepository<ReceiptItem>,
        IGetItemByIdRepository<ReceiptItem, Guid>,
        IGetItemByPredicateRepository<ReceiptItem>,
        IProjectedReadRepository<ReceiptItem>
    {
    }
}
