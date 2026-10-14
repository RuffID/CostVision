using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class ProductNamingUnitOfWork(
    IUnitOfWorkScope scope,
    IProductRepository product,
    IReceiptRepository receipt)
    : ScenarioUnitOfWork(scope), IProductNamingUnitOfWork
{
    public IProductRepository Product { get; } = product;
    public IReceiptRepository Receipt { get; } = receipt;
}
