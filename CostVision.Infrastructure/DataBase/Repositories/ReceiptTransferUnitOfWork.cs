using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class ReceiptTransferUnitOfWork(
    IUnitOfWorkScope scope,
    IReceiptRepository receipt,
    IReceiptAccountRepository receiptAccount,
    IAccountRepository account)
    : ScenarioUnitOfWork(scope), IReceiptTransferUnitOfWork
{
    public IReceiptRepository Receipt { get; } = receipt;
    public IReceiptAccountRepository ReceiptAccount { get; } = receiptAccount;
    public IAccountRepository Account { get; } = account;
}
