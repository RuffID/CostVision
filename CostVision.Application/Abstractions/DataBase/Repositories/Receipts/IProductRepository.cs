using CostVision.Application.Abstractions.DataBase;
using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IProductRepository :
        ICreateItemRepository<Product, AppDbContextBase>,
        IGetItemByIdRepository<Product, Guid, AppDbContextBase>,
        IGetItemByPredicateRepository<Product, AppDbContextBase>
    {
        Task<int> CountByPredicateAsync(Expression<Func<Product, bool>>? predicate = null, CancellationToken ct = default);
    }
}
