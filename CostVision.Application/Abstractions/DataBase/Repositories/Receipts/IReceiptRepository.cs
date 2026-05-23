using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IReceiptRepository :
        ICreateItemRepository<Receipt, AppDbContextBase>,
        IDeleteItemRepository<Receipt, AppDbContextBase>,
        IGetItemByIdRepository<Receipt, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<Receipt, AppDbContextBase>
    {
        Task<List<Receipt>> GetWithoutItemsAsync(CancellationToken ct = default);
    }
}
