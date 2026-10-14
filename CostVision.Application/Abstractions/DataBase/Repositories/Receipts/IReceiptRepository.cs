using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptRepository :
        ICreateItemRepository<Receipt>,
        IDeleteItemRepository<Receipt>,
        IGetItemByIdRepository<Receipt, Guid>,
        IGetItemByPredicateRepository<Receipt>,
        IProjectedReadRepository<Receipt>
    {
    }
}
