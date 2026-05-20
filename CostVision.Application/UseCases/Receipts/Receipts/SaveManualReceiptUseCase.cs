using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Helpers;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class SaveManualReceiptUseCase(IUnitOfWork unitOfWork, IReceiptRefreshWorkflow receiptRefreshWorkflow) : ISaveManualReceiptUseCase
    {
        public async Task<ManualReceiptResult> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct)
        {
            Receipt? existingReceiptInAccount = request.AccountId == Guid.Empty
                ? null
                : await FindExistingReceiptInAccountAsync(
                    request.Receipt.FiscalDocumentNumber,
                    request.Receipt.FiscalDriveNumber,
                    request.Receipt.FiscalSign,
                    request.Receipt.Sum,
                    request.Receipt.DateTime,
                    request.Receipt.OperationType,
                    request.AccountId,
                    ct);

            if (existingReceiptInAccount != null)
            {
                return new ManualReceiptResult
                {
                    IsCreated = false,
                    ErrorMessage = "Такой чек уже есть на выбранном счёте.",
                    Receipt = await LoadReceiptWithAccountsAsync(existingReceiptInAccount.Id, ct)
                };
            }

            Receipt? existingReceipt = await FindExistingReceiptAsync(
                request.Receipt.FiscalDocumentNumber,
                request.Receipt.FiscalDriveNumber,
                request.Receipt.FiscalSign,
                request.Receipt.Sum,
                request.Receipt.DateTime,
                request.Receipt.OperationType,
                currentUserId,
                ct);

            if (existingReceipt != null)
            {
                if (request.AccountId != Guid.Empty)
                {
                    unitOfWork.ReceiptAccount.Create(new ReceiptAccount
                    {
                        AccountId = request.AccountId,
                        ReceiptId = existingReceipt.Id
                    });

                    await UserActivityUpdater.MarkReceiptActivityAsync(unitOfWork, currentUserId, ct);
                    await unitOfWork.SaveChangesAsync(ct);

                    Receipt linkedReceipt = await LoadReceiptWithAccountsAsync(existingReceipt.Id, ct);

                    return new ManualReceiptResult
                    {
                        IsCreated = true,
                        ErrorMessage = "Чек уже существовал и был добавлен на выбранный счёт.",
                        Receipt = linkedReceipt
                    };
                }

                return new ManualReceiptResult
                {
                    IsCreated = false,
                    ErrorMessage = "Такой чек уже существует в базе.",
                    Receipt = await LoadReceiptWithAccountsAsync(existingReceipt.Id, ct)
                };
            }

            Receipt receipt = new()
            {
                FiscalDocumentNumber = request.Receipt.FiscalDocumentNumber,
                FiscalDriveNumber = request.Receipt.FiscalDriveNumber,
                FiscalSign = request.Receipt.FiscalSign,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                OperationType = request.Receipt.OperationType,
                DateTime = request.Receipt.DateTime,
                TotalSum = request.Receipt.Sum
            };

            if (request.AccountId != Guid.Empty)
            {
                ReceiptAccount link = new() { AccountId = request.AccountId, Receipt = receipt };
                receipt.Accounts.Add(link);
            }

            unitOfWork.Receipt.Create(receipt);
            await UserActivityUpdater.MarkReceiptActivityAsync(unitOfWork, currentUserId, ct);
            await unitOfWork.SaveChangesAsync(ct);
            Receipt refreshedReceipt = await receiptRefreshWorkflow.TryRefreshCreatedReceiptAsync(receipt, ct);

            return new ManualReceiptResult
            {
                IsCreated = true,
                Receipt = refreshedReceipt
            };
        }

        private Task<Receipt?> FindExistingReceiptAsync(string fiscalDocumentNumber, string fiscalDriveNumber, string fiscalSign, decimal totalSum, DateTime dateTime, ReceiptOperationType operationType, Guid currentUserId, CancellationToken ct)
        {
            return unitOfWork.Receipt.GetItemByPredicateAsync(
                r => r.FiscalDocumentNumber == fiscalDocumentNumber &&
                     r.FiscalDriveNumber == fiscalDriveNumber &&
                     r.FiscalSign == fiscalSign &&
                     r.TotalSum == totalSum &&
                     r.DateTime == dateTime &&
                     r.CreatedByUserId == currentUserId &&
                     r.OperationType == operationType,
                asNoTracking: true,
                ct: ct);
        }

        private Task<Receipt?> FindExistingReceiptInAccountAsync(string fiscalDocumentNumber, string fiscalDriveNumber, string fiscalSign, decimal totalSum, DateTime dateTime, ReceiptOperationType operationType, Guid accountId, CancellationToken ct)
        {
            return unitOfWork.Receipt.GetItemByPredicateAsync(
                r => r.FiscalDocumentNumber == fiscalDocumentNumber &&
                     r.FiscalDriveNumber == fiscalDriveNumber &&
                     r.FiscalSign == fiscalSign &&
                     r.TotalSum == totalSum &&
                     r.DateTime == dateTime &&
                     r.OperationType == operationType &&
                     r.Accounts.Any(link => link.AccountId == accountId),
                asNoTracking: true,
                ct: ct);
        }

        private async Task<Receipt> LoadReceiptWithAccountsAsync(Guid receiptId, CancellationToken ct)
        {
            Receipt? receipt = await unitOfWork.Receipt.GetByIdWithAccountsAsync(receiptId, asNoTracking: true, ct: ct);

            return receipt ?? throw new InvalidOperationException("Не удалось загрузить чек после сохранения.");
        }
    }
}
