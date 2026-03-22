using CostVision.Abstractions.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IProductRepository : IGetItemByIdRepository<Product, Guid>, IGetItemByPredicateRepository<Product>, ICreateItemRepository<Product>
    {
    }
}