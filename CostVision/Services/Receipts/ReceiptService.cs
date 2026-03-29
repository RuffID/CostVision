using CostVision.Abstractions.Api;
using CostVision.Abstractions.DataBase.Repositories;
using CostVision.Abstractions.Service.Receipts;
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
            List<Receipt> createdReceipts = new();

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

                Receipt? exist = await FindExistingReceiptAsync(
                    result.Parsed.FiscalDocumentNumber,
                    result.Parsed.FiscalDriveNumber,
                    result.Parsed.FiscalSign,
                    result.Parsed.Sum.Value,
                    result.Parsed.DateTime,
                    (ReceiptOperationType)result.Parsed.OperationType.Value,
                    currentUserId,
                    ct);

                if (exist != null)
                {
                    if (request.AccountId == Guid.Empty)
                        continue;

                    unitOfWork.ReceiptAccount.Create(new ReceiptAccount
                    {
                        AccountId = request.AccountId,
                        ReceiptId = exist.Id
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
                await unitOfWork.SaveChangesAsync(ct);
                await TryRefreshCreatedReceiptsAsync(createdReceipts, ct);
            }

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

            Receipt? exist = await FindExistingReceiptAsync(
                request.Receipt.FiscalDocumentNumber,
                request.Receipt.FiscalDriveNumber,
                request.Receipt.FiscalSign,
                request.Receipt.Sum,
                request.Receipt.DateTime,
                request.Receipt.OperationType,
                currentUserId,
                ct);

            if (exist != null)
            {
                if (request.AccountId != Guid.Empty)
                {
                    unitOfWork.ReceiptAccount.Create(new ReceiptAccount
                    {
                        AccountId = request.AccountId,
                        ReceiptId = exist.Id
                    });

                    await unitOfWork.SaveChangesAsync(ct);

                    Receipt linkedReceipt = await LoadReceiptWithAccountsAsync(exist.Id, ct);

                    return new ManualReceiptResult
                    {
                        IsCreated = true,
                        ErrorMessage = "Чек уже существовал и был добавлен на выбранный счёт.",
                        Receipt = linkedReceipt
                    };
                }

                ManualReceiptResult duplicateResult = new()
                {
                    IsCreated = false,
                    ErrorMessage = "Такой чек уже существует в базе.",
                    Receipt = await LoadReceiptWithAccountsAsync(exist.Id, ct)
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
            Receipt refreshedReceipt = await TryRefreshCreatedReceiptAsync(receipt, ct);

            ManualReceiptResult result = new()
            {
                IsCreated = true,
                Receipt = refreshedReceipt
            };

            return result;
        }

        public async Task<ServiceResult<List<Receipt>>> GetReceiptListAsync(User currentUser, DateTime dateFrom, DateTime dateTo, CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(r =>
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

            Receipt? receipt = await unitOfWork.Receipt.GetItemByIdAsync(id: receiptId, asNoTracking: false, include: q => q
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

            Receipt? receipt = await unitOfWork.Receipt.GetItemByIdAsync(receiptId, asNoTracking: false, include: q => q
                .Include(r => r.Items)
                .Include(r => r.Accounts)
                    .ThenInclude(ra => ra.Account)
                        .ThenInclude(a => a!.Members)
                .AsSplitQuery(), ct: ct);

            if (receipt == null)
                return ServiceResult<Receipt>.Fail(404, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<Receipt>.Fail(401, "Нет доступа к этому чеку.");

            return await RefreshReceiptFromApiInternalAsync(receipt, ct);
        }

        public async Task RefreshReceiptsWithoutItemsAsync(CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicateAsync(r => !r.Items.Any(), include: q => q.Include(r => r.Items), ct: ct);

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
                        Product? productFromDb = await unitOfWork.Product.GetItemByPredicateAsync(p => normalizedName == p.NormalizedName, ct: ct);

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

            Receipt? receipt = await unitOfWork.Receipt.GetItemByIdAsync(id: receiptId, asNoTracking: false, ct: ct);

            if (receipt == null)
                return ServiceResult<bool>.Fail(404, "Чек не найден.");

            if (receipt.CreatedByUserId != currentUser.Id)
                return ServiceResult<bool>.Fail(403, "Можно удалять только собственный чек.");

            unitOfWork.Receipt.Delete(receipt);

            await unitOfWork.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }

        public string BuildReceiptIdentityKey(Receipt receipt)
        {
            return string.Join('|',
                receipt.FiscalDriveNumber,
                receipt.FiscalDocumentNumber,
                receipt.FiscalSign,
                receipt.DateTime.Ticks,
                receipt.TotalSum,
                (int)receipt.OperationType);
        }

        private Task<Receipt?> FindExistingReceiptAsync(
            string fiscalDocumentNumber,
            string fiscalDriveNumber,
            string fiscalSign,
            decimal totalSum,
            DateTime dateTime,
            ReceiptOperationType operationType,
            Guid currentUserId,
            CancellationToken ct)
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

        private Task<Receipt?> FindExistingReceiptInAccountAsync(
            string fiscalDocumentNumber,
            string fiscalDriveNumber,
            string fiscalSign,
            decimal totalSum,
            DateTime dateTime,
            ReceiptOperationType operationType,
            Guid accountId,
            CancellationToken ct)
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
            Receipt? receipt = await unitOfWork.Receipt.GetItemByIdAsync(
                receiptId,
                asNoTracking: true,
                include: query => query
                    .Include(r => r.Accounts)
                        .ThenInclude(ra => ra.Account)
                    .AsSplitQuery(),
                ct: ct);

            return receipt ?? throw new InvalidOperationException("Не удалось загрузить чек после сохранения.");
        }

        private async Task TryRefreshCreatedReceiptsAsync(IEnumerable<Receipt> receipts, CancellationToken ct)
        {
            foreach (Receipt receipt in receipts)            
                await TryRefreshCreatedReceiptAsync(receipt, ct);            
        }

        private async Task<Receipt> TryRefreshCreatedReceiptAsync(Receipt receipt, CancellationToken ct)
        {
            try
            {
                ServiceResult<Receipt> refreshResult = await RefreshReceiptFromApiInternalAsync(receipt, ct);
                return refreshResult.Success && refreshResult.Data != null ? refreshResult.Data : receipt;
            }
            catch
            {
                return receipt;
            }
        }
    }
}
