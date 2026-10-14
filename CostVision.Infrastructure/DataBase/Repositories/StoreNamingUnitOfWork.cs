using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class StoreNamingUnitOfWork(
    IUnitOfWorkScope scope,
    IStoreRepository store,
    IReceiptRepository receipt)
    : ScenarioUnitOfWork(scope), IStoreNamingUnitOfWork
{
    public IStoreRepository Store { get; } = store;
    public IReceiptRepository Receipt { get; } = receipt;
}
