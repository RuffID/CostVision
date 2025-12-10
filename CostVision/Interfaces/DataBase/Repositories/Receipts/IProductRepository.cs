using CostVision.Interfaces.DataBase.Repositories.Base;
using CostVision.Models.Receipts;

namespace CostVision.Interfaces.DataBase.Repositories.Receipts
{
    public interface IProductRepository : IGetItemByIdRepository<Product, Guid>, IGetItemByPredicateRepository<Product>, ICreateItemRepository<Product>, IUpsertItemByIdRepository<Product, Guid>, IUpsertItemByPredicateRepository<Product>
    {
    }
}