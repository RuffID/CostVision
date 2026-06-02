using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IStoreRepository :
        ICreateItemRepository<Store, AppDbContextBase>,
        IGetItemByIdRepository<Store, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<Store, AppDbContextBase>
    {
        Task<int> CountByPredicateAsync(Expression<Func<Store, bool>>? predicate = null, CancellationToken ct = default);
    }
}
