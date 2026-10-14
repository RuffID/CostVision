using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using System.Linq.Expressions;

namespace CostVision.Application.Abstractions.DataBase.Repositories.Receipts
{
    public interface IProductRepository :
        ICreateItemRepository<Product>,
        IGetItemByIdRepository<Product, Guid>,
        IGetItemByPredicateRepository<Product>,
        IProjectedReadRepository<Product>
    {
        Task<int> CountByPredicateAsync(Expression<Func<Product, bool>>? predicate = null, CancellationToken ct = default);
    }
}
