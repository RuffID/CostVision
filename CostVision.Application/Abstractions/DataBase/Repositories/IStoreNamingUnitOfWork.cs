using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Репозитории атомарного сценария; сохранение и отказ принадлежат общему scope.
/// </summary>
public interface IStoreNamingUnitOfWork : IUnitOfWorkScope
{
    IStoreRepository Store { get; }
    IReceiptRepository Receipt { get; }
}
