using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class ReceiptCreationUnitOfWork(
    IUnitOfWorkScope scope,
    IReceiptRepository receipt,
    IReceiptAccountRepository receiptAccount)
    : ScenarioUnitOfWork(scope), IReceiptCreationUnitOfWork
{
    public IReceiptRepository Receipt { get; } = receipt;
    public IReceiptAccountRepository ReceiptAccount { get; } = receiptAccount;
}
