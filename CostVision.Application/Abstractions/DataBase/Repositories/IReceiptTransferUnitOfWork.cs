using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Репозитории атомарного сценария; сохранение и отказ принадлежат общему scope.
/// </summary>
public interface IReceiptTransferUnitOfWork : IUnitOfWorkScope
{
    IReceiptRepository Receipt { get; }
    IReceiptAccountRepository ReceiptAccount { get; }
    IAccountRepository Account { get; }
}
