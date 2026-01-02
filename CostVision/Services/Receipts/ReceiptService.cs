using CostVision.Interfaces.Api;
using CostVision.Interfaces.DataBase.Repositories;
using CostVision.Interfaces.Service.Receipts;
using CostVision.Models.Authorization;
using CostVision.Models.ConfigClass;
using CostVision.Models.Dtos.Mappers;
using CostVision.Models.Enums.Receipts;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.ProverkachekaApi;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Responses.ProverkachekaApi;
using CostVision.Models.Responses.Results;
using CostVision.Models.Services.Receipts;
using CostVision.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CostVision.Services.Receipts
{
    public class ReceiptService(IUnitOfWork unitOfWork, ILogger<ReceiptService> logger, QrParser qrParser, IReceiptRequest receiptRequest, IReceiptAccessVerificationService accessVerification, IOptions<ApiEndpointOptions> endpointOptions, IOptions<ProverkachekaOptions> apiOptions) : IReceiptService
    {
        private readonly ApiEndpointOptions _endpointOptions = endpointOptions.Value;
        private readonly ProverkachekaOptions _apiOptions = apiOptions.Value;

        public async Task<ReceiptScanResultSummary> SaveReceiptsScannedAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct)
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
                addedCount++;
            }


            if (addedCount > 0)
                await unitOfWork.SaveChangesAsync(ct);

            int errorCount = request.Results.Count(r => !string.IsNullOrWhiteSpace(r.ErrorMessage));

            ReceiptScanResultSummary summary = new()
            {
                ScannedCount = scannedCount,
                AddedToDbCount = addedCount,
                ErrorCount = errorCount,
                Results = request.Results
            };

            return summary;
        }

        public async Task<ManualReceiptResult> SaveReceiptManualAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct)
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
                ManualReceiptResult duplicateResult = new()
                {
                    IsCreated = false,
                    ErrorMessage = "Такой чек уже существует в базе.",
                    Receipt = exist
                };

                return duplicateResult;
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
            await unitOfWork.SaveChangesAsync(ct);

            ManualReceiptResult result = new()
            {
                IsCreated = true,
                Receipt = receipt
            };

            return result;
        }

        public async Task<ServiceResult<List<Receipt>>> GetReceiptListAsync(User currentUser, DateTime dateFrom, DateTime dateTo, CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicate(r =>
                r.DateTime >= dateFrom &&
                r.DateTime <= dateTo &&
                (
                    r.CreatedByUserId == currentUser.Id ||
                    r.Accounts.Any(a => a.Account!.CreatedByUserId == currentUser.Id) ||
                    r.Accounts.Any(a => a.Account!.Members.Any(m => m.UserId == currentUser.Id))
                ),
                asNoTracking: true, include: r => r
                    .Include(r => r.Accounts)
                        .ThenInclude(ra => ra.Account)
                            .ThenInclude(a => a!.Members)
                    .AsSplitQuery(), ct: ct);

            return ServiceResult<List<Receipt>>.Ok(receipts);
        }

        public async Task<ServiceResult<Receipt>> GetReceiptWithItemsAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<Receipt>.Fail(400, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemById(id: receiptId, asNoTracking: false, include: q => q
                    .Include(r => r.Items)
                        .ThenInclude(i => i.Product)
                    .Include(r => r.Accounts)
                        .ThenInclude(ra => ra.Account)
                            .ThenInclude(a => a!.Members)
                    .AsSplitQuery(), ct);

            if (receipt == null)
                return ServiceResult<Receipt>.Fail(404, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<Receipt>.Fail(401, "Нет доступа к этому чеку.");

            return ServiceResult<Receipt>.Ok(receipt);
        }

        public async Task<ServiceResult<Receipt>> RefreshReceiptFromApiAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<Receipt>.Fail(400, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemById(receiptId, asNoTracking: false, include: r => r.Include(x => x.Items), ct: ct);

            if (receipt == null)
                return ServiceResult<Receipt>.Fail(404, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<Receipt>.Fail(401, "Нет доступа к этому чеку.");

            return await RefreshReceiptFromApiInternalAsync(receipt, ct);
        }

        public async Task RefreshReceiptsWithoutItemsAsync(CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicate(r => !r.Items.Any(), include: q => q.Include(r => r.Items), ct: ct);

            int countUpdatedReceipts = 0;
            foreach (Receipt receipt in receipts)
            {
                // Задержка, чтобы не посылать запросы в api слишком быстро
                await Task.Delay(1000, ct);
                ServiceResult<Receipt> result = await RefreshReceiptFromApiInternalAsync(receipt, ct);

                if (result.Success)
                    countUpdatedReceipts++;
            }

            if (countUpdatedReceipts > 0)
                logger.LogInformation("[Method:{MethodName}] Refreshed {Count} receipts without items from external API.", nameof(RefreshReceiptsWithoutItemsAsync), countUpdatedReceipts);
        }

        private async Task<ServiceResult<Receipt>> RefreshReceiptFromApiInternalAsync(Receipt receipt, CancellationToken ct)
        {
            ProverkachekaManualRequest apiRequest = new()
            {
                ApiToken = _apiOptions.ProverkachekaApiToken,
                Fd = receipt.FiscalDocumentNumber,
                Fn = receipt.FiscalDriveNumber,
                Fp = receipt.FiscalSign,
                Time = receipt.DateTime.ToString("yyyyMMddTHHmm"),
                Summ = receipt.TotalSum.ToString(),
                OperationType = (int)receipt.OperationType
            };

            ProverkachekaResponse? result = await receiptRequest.GetReceiptByReceiptAsync(_endpointOptions.ProverkachekaApiUrl, apiRequest, ct);

            if (result == null)
                return ServiceResult<Receipt>.Fail(500, "Не удалось обновить чек по внешнему API. \nОшибка при получении ответа.");

            if (result.Code == (int)ReceiptResponseCodeEnum.Incorrect)
            {
                logger.LogWarning("[Method:{MethodName}] Receipt with Id {ReceiptId} could not be refreshed from API because it is marked as incorrect.", nameof(RefreshReceiptFromApiInternalAsync), receipt.Id);
                return ServiceResult<Receipt>.Fail(500, $"Не удалось обновить чек по внешнему API. \nОшибка: {result.Data?.Error}");
            }

            if (result.Code == (int)ReceiptResponseCodeEnum.WaitBeforeRetry ||
                result.Code == (int)ReceiptResponseCodeEnum.RateLimitExceeded ||
                result.Code == (int)ReceiptResponseCodeEnum.Pending ||
                result.Code == (int)ReceiptResponseCodeEnum.Other)
            {
                return ServiceResult<Receipt>.Fail(500, $"Не удалось обновить чек по внешнему API. \nОшибка: {result.Data?.Error}");
            }

            receipt.CopyData(result.MapToReceipt());
            receipt.UpdatedAtUtc = DateTime.UtcNow;

            // Очистить чек от старых позиций (если они были)
            receipt.Items.Clear();

            Dictionary<string, Product> productCache = new(StringComparer.Ordinal);

            if (result.Data != null && result.Data.Json != null)
            {
                foreach (ProverkachekaItem item in result.Data.Json.Items)
                {
                    string normalizedName = NameNormalizedHelper.GetNormalizedName(item.Name);

                    Product product;
                    if (productCache.TryGetValue(normalizedName, out Product? cached))
                    {
                        product = cached;
                        product.Name = item.Name;
                        product.ProductCode = item.ProductCode?.RawProductCode;
                    }
                    else
                    {
                        Product? productFromDb = await unitOfWork.Product.GetItemByPredicate(p => normalizedName == p.NormalizedName, ct: ct);

                        if (productFromDb == null)
                        {
                            product = new Product
                            {
                                Name = item.Name,
                                NormalizedName = normalizedName,
                                ProductCode = item.ProductCode?.RawProductCode
                            };
                            unitOfWork.Product.Create(product);
                        }
                        else
                        {
                            product = productFromDb;
                            product.Name = item.Name;
                            product.ProductCode = item.ProductCode?.RawProductCode;
                        }

                        productCache[normalizedName] = product;
                    }
                        ReceiptItem receiptItem = item.MapToReceiptItem(receipt.Id, product.Id);
                        receiptItem.Product = product;
                        receipt.Items.Add(receiptItem);                   
                }
            }

            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult<Receipt>.Ok(receipt);
        }

        public async Task<ServiceResult<bool>> DeleteReceiptAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<bool>.Fail(400, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetItemById(id: receiptId, asNoTracking: false, ct: ct);

            if (receipt == null)
                return ServiceResult<bool>.Fail(404, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<bool>.Fail(401, "Нет доступа к этому чеку.");

            unitOfWork.Receipt.Delete(receipt);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
    }
}
