using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Репозитории атомарного сценария; сохранение и отказ принадлежат общему scope.
/// </summary>
public interface IReceiptRefreshUnitOfWork : IUnitOfWorkScope
{
    IReceiptRepository Receipt { get; }
    IProductRepository Product { get; }
    IStoreRepository Store { get; }
}
