using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.Service.Receipt;
using CostVision.Models.Enums.Document;
using CostVision.Models.Receipts;
using CostVision.Models.Services.Receipts;

namespace CostVision.Services.Receipts
{
    public class ReceiptService(IUnitOfWork unitOfWork, QrParser qrParser) : IReceiptService
    {
        public async Task<ReceiptScanResultSummary> SaveScannedReceiptsAsync(List<QrScanResult> results, Guid currentUserId, CancellationToken ct)
        {
            // Считать количество отсканированных чеков
            int scannedCount = results.Count(r => !string.IsNullOrWhiteSpace(r.DecodedText));
            int addedCount = 0;

            foreach (QrScanResult result in results)
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

                unitOfWork.Receipt.Create(receipt);
                addedCount++;
            }

            if (addedCount > 0)
                await unitOfWork.SaveAsync(ct);

            int errorCount = results.Count(r => !string.IsNullOrWhiteSpace(r.ErrorMessage));

            ReceiptScanResultSummary summary = new ()
            {
                ScannedCount = scannedCount,
                AddedToDbCount = addedCount,
                ErrorCount = errorCount,
                Results = results
            };

            return summary;
        }

        public async Task<ManualReceiptResult> SaveManualReceiptAsync(ManualReceiptInput input, Guid currentUserId, CancellationToken ct)
        {
            // Проверять дубликат по тем же полям, что и при скане
            Receipt? exist = await unitOfWork.Receipt.GetItemByPredicate(
                r => r.FiscalDocumentNumber == input.FiscalDocumentNumber &&
                     r.FiscalDriveNumber == input.FiscalDriveNumber &&
                     r.FiscalSign == input.FiscalSign &&
                     r.TotalSum == input.Sum &&
                     r.DateTime == input.DateTime &&
                     r.CreatedByUserId == currentUserId &&
                     r.OperationType == input.OperationType,
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

            Receipt manualReceipt = new ()
            {
                FiscalDocumentNumber = input.FiscalDocumentNumber,
                FiscalDriveNumber = input.FiscalDriveNumber,
                FiscalSign = input.FiscalSign,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = currentUserId,
                OperationType = input.OperationType,
                DateTime = input.DateTime,
                TotalSum = input.Sum
            };

            unitOfWork.Receipt.Create(manualReceipt);
            await unitOfWork.SaveAsync(ct);

            ManualReceiptResult result = new ()
            {
                IsCreated = true,
                Receipt = manualReceipt
            };

            return result;
        }
    }
}
