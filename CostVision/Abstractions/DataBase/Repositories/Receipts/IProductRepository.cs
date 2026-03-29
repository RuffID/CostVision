using CostVision.DataBase;
using CostVision.Models.Receipts;
using EFCoreLibrary.Abstractions.Database.Repository.Base;

namespace CostVision.Abstractions.DataBase.Repositories.Receipts
{
    public interface IProductRepository : IGetItemByIdRepository<Product, Guid, ApplicationContext>, IGetItemByPredicateRepository<Product, ApplicationContext>, ICreateItemRepository<Product, ApplicationContext>
    {
    }
}