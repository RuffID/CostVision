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
        Task<Receipt?> GetByIdWithAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<Receipt?> GetByIdWithAccountsAndMembersAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<Receipt?> GetByIdWithItemsAndAccountsAsync(Guid id, bool asNoTracking = false, CancellationToken ct = default);

        Task<List<Receipt>> GetAccessibleByPeriodAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default);

        Task<List<Receipt>> GetWithoutItemsAsync(CancellationToken ct = default);
    }
}
