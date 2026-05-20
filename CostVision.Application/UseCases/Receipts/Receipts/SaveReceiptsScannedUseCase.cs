using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Helpers;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class SaveReceiptsScannedUseCase(IUnitOfWork unitOfWork, IQrParser qrParser, IReceiptRefreshWorkflow receiptRefreshWorkflow) : ISaveReceiptsScannedUseCase
    {
        public async Task<ReceiptScanResultSummary> ExecuteAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct)
        {
            int scannedCount = request.Results.Count(r => !string.IsNullOrWhiteSpace(r.DecodedText));
            int addedCount = 0;
            List<Receipt> createdReceipts = new();

            foreach (QrScanResult result in request.Results)
            {
                if (string.IsNullOrEmpty(result.DecodedText))
                    continue;

                result.Parsed = qrParser.Parse(result.DecodedText);

                if (result.Parsed == null)
                {
                    result.ErrorMessage = "Не удалось разобрать данные QR-кода.";
                    continue;
                }

                if (string.IsNullOrEmpty(result.Parsed.FiscalDocumentNumber) ||
                    string.IsNullOrEmpty(result.Parsed.FiscalDriveNumber) ||
                    string.IsNullOrEmpty(result.Parsed.FiscalSign) ||
                    result.Parsed.DateTime == DateTime.MinValue ||
                    result.Parsed.Sum == null ||
                    result.Parsed.OperationType == null)
                {
                    result.ErrorMessage = "В данных QR-кода отсутствуют обязательные поля фискального чека.";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(result.FileName))
                    result.FileName = "Browser file";

                Receipt? existingReceiptInAccount = request.AccountId == Guid.Empty
                    ? null
                    : await FindExistingReceiptInAccountAsync(
                        result.Parsed.FiscalDocumentNumber,
                        result.Parsed.FiscalDriveNumber,
                        result.Parsed.FiscalSign,
                        result.Parsed.Sum.Value,
                        result.Parsed.DateTime,
                        (ReceiptOperationType)result.Parsed.OperationType.Value,
                        request.AccountId,
                        ct);

                if (existingReceiptInAccount != null)
                {
                    result.ErrorMessage = "Такой чек уже есть на выбранном счёте.";
                    continue;
                }

                Receipt? existingReceipt = await FindExistingReceiptAsync(
                    result.Parsed.FiscalDocumentNumber,
                    result.Parsed.FiscalDriveNumber,
                    result.Parsed.FiscalSign,
                    result.Parsed.Sum.Value,
                    result.Parsed.DateTime,
                    (ReceiptOperationType)result.Parsed.OperationType.Value,
                    currentUserId,
                    ct);

                if (existingReceipt != null)
                {
                    if (request.AccountId == Guid.Empty)
                        continue;

                    unitOfWork.ReceiptAccount.Create(new ReceiptAccount
                    {
                        AccountId = request.AccountId,
                        ReceiptId = existingReceipt.Id
                    });

                    addedCount++;
                    continue;
                }

                Receipt receipt = new()
                {
                    FiscalDocumentNumber = result.Parsed.FiscalDocumentNumber,
                    FiscalDriveNumber = result.Parsed.FiscalDriveNumber,
                    FiscalSign = result.Parsed.FiscalSign,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedByUserId = currentUserId,
                    OperationType = (ReceiptOperationType)result.Parsed.OperationType.Value,
                    DateTime = result.Parsed.DateTime,
                    TotalSum = result.Parsed.Sum.Value
                };

                if (request.AccountId != Guid.Empty)
                {
                    ReceiptAccount link = new() { AccountId = request.AccountId, Receipt = receipt };
                    receipt.Accounts.Add(link);
                }

                unitOfWork.Receipt.Create(receipt);
                createdReceipts.Add(receipt);
                addedCount++;
            }

            if (addedCount > 0)
            {
                await UserActivityUpdater.MarkReceiptActivityAsync(unitOfWork, currentUserId, ct);
                await unitOfWork.SaveChangesAsync(ct);
                await receiptRefreshWorkflow.TryRefreshCreatedReceiptsAsync(createdReceipts, ct);
            }

            int errorCount = request.Results.Count(r => !string.IsNullOrWhiteSpace(r.ErrorMessage));

            return new ReceiptScanResultSummary
            {
                ScannedCount = scannedCount,
                AddedToDbCount = addedCount,
                ErrorCount = errorCount,
                Results = request.Results
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
    }
}
