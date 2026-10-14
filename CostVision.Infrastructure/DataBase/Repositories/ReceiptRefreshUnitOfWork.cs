using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class ReceiptRefreshUnitOfWork(
    IUnitOfWorkScope scope,
    IReceiptRepository receipt,
    IProductRepository product,
    IStoreRepository store)
    : ScenarioUnitOfWork(scope), IReceiptRefreshUnitOfWork
{
    public IReceiptRepository Receipt { get; } = receipt;
    public IProductRepository Product { get; } = product;
    public IStoreRepository Store { get; } = store;
}
