using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Products;

namespace CostVision.Interfaces.DataBase.Repositories.Products
{
    public interface IProductRepository : IGetItemByIdRepository<Product, Guid>, IGetItemByPredicateRepository<Product>, ICreateItemRepository<Product>, IUpsertItemByIdRepository<Product, Guid>
    {
    }
}