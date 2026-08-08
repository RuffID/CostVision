using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts
{
    public class SaveReceiptsScannedUseCase(
        IUnitOfWork unitOfWork,
        IValidateReceiptCreationAccessUseCase validateReceiptCreationAccessUseCase,
        IQrParser qrParser,
        IReceiptRefreshWorkflow receiptRefreshWorkflow) : ISaveReceiptsScannedUseCase
    {
        public async Task<ServiceResult<ReceiptScanResultSummary>> ExecuteAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct)
        {
            if (request.Results.Count == 0)
                return ServiceResult<ReceiptScanResultSummary>.Fail(400, "Нет данных для обработки.");

            if (request.AccountId != Guid.Empty)
            {
                ServiceResult<bool> accessResult = await validateReceiptCreationAccessUseCase.ExecuteAsync(request.AccountId, currentUserId, ct);
                if (!accessResult.Success)
                    return ServiceResult<ReceiptScanResultSummary>.Fail(accessResult.Error!.StatusCode, accessResult.Error.Message);
            }

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

                if (!Receipt.TryCreate(
                        result.Parsed.FiscalDriveNumber,
                        result.Parsed.FiscalDocumentNumber,
                        result.Parsed.FiscalSign,
                        result.Parsed.DateTime,
                        (ReceiptOperationType)result.Parsed.OperationType.Value,
                        result.Parsed.Sum.Value,
                        currentUserId,
                        DateTime.UtcNow,
                        out Receipt? createdReceipt,
                        out string? creationError))
                {
                    result.ErrorMessage = creationError;
                    continue;
                }

                Receipt receipt = createdReceipt!;
                Receipt? existingReceiptInAccount = request.AccountId == Guid.Empty
                    ? null
                    : await FindExistingReceiptInAccountAsync(
                        receipt.FiscalDocumentNumber,
                        receipt.FiscalDriveNumber,
                        receipt.FiscalSign,
                        receipt.TotalSum,
                        receipt.DateTime,
                        receipt.OperationType,
                        request.AccountId,
                        ct);

                if (existingReceiptInAccount != null)
                {
                    result.ErrorMessage = "Такой чек уже есть на выбранном счёте.";
                    continue;
                }

                Receipt? existingReceipt = await FindExistingReceiptAsync(
                    receipt.FiscalDocumentNumber,
                    receipt.FiscalDriveNumber,
                    receipt.FiscalSign,
                    receipt.TotalSum,
                    receipt.DateTime,
                    receipt.OperationType,
                    currentUserId,
                    ct);

                if (existingReceipt != null)
                {
                    if (request.AccountId == Guid.Empty)
                        continue;

                    if (!existingReceipt.TryAddAccount(request.AccountId, out ReceiptAccount? link, out string? linkError))
                    {
                        result.ErrorMessage = linkError;
                        continue;
                    }

                    unitOfWork.ReceiptAccount.Create(link!);
                    addedCount++;
                    continue;
                }

                if (request.AccountId != Guid.Empty)
                {
                    if (!receipt.TryAddAccount(request.AccountId, out _, out string? linkError))
                    {
                        result.ErrorMessage = linkError;
                        continue;
                    }
                }

                unitOfWork.Receipt.Create(receipt);
                createdReceipts.Add(receipt);
                addedCount++;
            }

            if (addedCount > 0)
            {
                await unitOfWork.SaveChangesAsync(ct);
                await receiptRefreshWorkflow.TryRefreshCreatedReceiptsAsync(createdReceipts, ct);
            }

            int errorCount = request.Results.Count(r => !string.IsNullOrWhiteSpace(r.ErrorMessage));

            return ServiceResult<ReceiptScanResultSummary>.Ok(new ReceiptScanResultSummary
            {
                ScannedCount = scannedCount,
                AddedToDbCount = addedCount,
                ErrorCount = errorCount,
                Results = request.Results
            });
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
                asNoTracking: false,
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
