using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.Service.Receipt;
using CostVision.Models.Enums.Document;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Services.Receipts;

namespace CostVision.Services.Receipts
{
    public class ReceiptService(IUnitOfWork unitOfWork, QrParser qrParser) : IReceiptService
    {
        public async Task<ReceiptScanResultSummary> SaveScannedReceiptsAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct)
        {
            // Считать количество отсканированных чеков
            int scannedCount = request.Results.Count(r => !string.IsNullOrWhiteSpace(r.DecodedText));
            int addedCount = 0;

            foreach (QrScanResult result in request.Results)
            {
                if (string.IsNullOrEmpty(result.DecodedText))
                    continue;

                // Разбирает строку QR
                result.Parsed = qrParser.Parse(result.DecodedText);

                if (result.Parsed == null)
                {
                    result.ErrorMessage = "Не удалось разобрать данные QR-кода.";
                    continue;
                }

                // Проверяет обязательные поля
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

                // Проверяет дубликат существующего чека
                Receipt? exist = await unitOfWork.Receipt.GetItemByPredicate(
                    r => r.FiscalDocumentNumber == result.Parsed.FiscalDocumentNumber &&
                         r.FiscalDriveNumber == result.Parsed.FiscalDriveNumber &&
                         r.FiscalSign == result.Parsed.FiscalSign &&
                         r.TotalSum == result.Parsed.Sum &&
                         r.DateTime == result.Parsed.DateTime &&
                         r.CreatedByUserId == currentUserId &&
                         r.OperationType == (ReceiptOperationType)result.Parsed.OperationType.Value,
                    asNoTracking: true,
                    ct: ct);

                if (exist != null)
                    continue;

                Receipt receipt = new ()
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
                addedCount++;
            }


            if (addedCount > 0)
                await unitOfWork.SaveAsync(ct);

            int errorCount = request.Results.Count(r => !string.IsNullOrWhiteSpace(r.ErrorMessage));

            ReceiptScanResultSummary summary = new ()
            {
                ScannedCount = scannedCount,
                AddedToDbCount = addedCount,
                ErrorCount = errorCount,
                Results = request.Results
            };

            return summary;
        }

        public async Task<ManualReceiptResult> SaveManualReceiptAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct)
        {
            // Проверять дубликат по тем же полям, что и при скане
            Receipt? exist = await unitOfWork.Receipt.GetItemByPredicate(
                r => r.FiscalDocumentNumber == request.Receipt.FiscalDocumentNumber &&
                     r.FiscalDriveNumber == request.Receipt.FiscalDriveNumber &&
                     r.FiscalSign == request.Receipt.FiscalSign &&
                     r.TotalSum == request.Receipt.Sum &&
                     r.DateTime == request.Receipt.DateTime &&
                     r.CreatedByUserId == currentUserId &&
                     r.OperationType == request.Receipt.OperationType,
                asNoTracking: true,
                ct: ct);

            if (exist != null)
            {
                ManualReceiptResult duplicateResult = new ()
                {
                    IsCreated = false,
                    ErrorMessage = "Такой чек уже существует в базе.",
                    Receipt = exist
                };

                return duplicateResult;
            }

            Receipt receipt = new ()
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
            await unitOfWork.SaveAsync(ct);

            ManualReceiptResult result = new ()
            {
                IsCreated = true,
                Receipt = receipt
            };

            return result;
        }
    }
}
