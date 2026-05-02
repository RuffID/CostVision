using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using Microsoft.Extensions.Logging;

namespace CostVision.Application.UseCases.Receipts
{
    public interface ISaveReceiptsScannedUseCase
    {
        Task<ReceiptScanResultSummary> ExecuteAsync(QrScanRequest request, Guid currentUserId, CancellationToken ct);
    }

    public interface ISaveManualReceiptUseCase
    {
        Task<ManualReceiptResult> ExecuteAsync(ReceiptManualCreateRequest request, Guid currentUserId, CancellationToken ct);
    }

    public interface IGetReceiptListUseCase
    {
        Task<ServiceResult<List<Receipt>>> ExecuteAsync(User currentUser, DateTime dateFrom, DateTime dateTo, CancellationToken ct);
    }

    public interface IGetReceiptWithItemsUseCase
    {
        Task<ServiceResult<Receipt>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }

    public interface IRefreshReceiptFromApiUseCase
    {
        Task<ServiceResult<Receipt>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }

    public interface IRefreshReceiptsWithoutItemsUseCase
    {
        Task ExecuteAsync(CancellationToken ct);
    }

    public interface IDeleteReceiptUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct);
    }

    public interface IReceiptRefreshWorkflow
    {
        Task<ServiceResult<Receipt>> RefreshAsync(Receipt receipt, CancellationToken ct);

        Task<Receipt> TryRefreshCreatedReceiptAsync(Receipt receipt, CancellationToken ct);

        Task TryRefreshCreatedReceiptsAsync(IEnumerable<Receipt> receipts, CancellationToken ct);
    }

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

    public class GetReceiptListUseCase(IUnitOfWork unitOfWork) : IGetReceiptListUseCase
    {
        public async Task<ServiceResult<List<Receipt>>> ExecuteAsync(User currentUser, DateTime dateFrom, DateTime dateTo, CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetAccessibleByPeriodAsync(currentUser.Id, dateFrom, dateTo, ct);

            return ServiceResult<List<Receipt>>.Ok(receipts);
        }
    }

    public class GetReceiptWithItemsUseCase(IUnitOfWork unitOfWork, IReceiptAccessVerificationService accessVerification) : IGetReceiptWithItemsUseCase
    {
        public async Task<ServiceResult<Receipt>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<Receipt>.Fail(400, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetByIdWithItemsAndAccountsAsync(receiptId, asNoTracking: false, ct: ct);

            if (receipt == null)
                return ServiceResult<Receipt>.Fail(404, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<Receipt>.Fail(401, "Нет доступа к этому чеку.");

            return ServiceResult<Receipt>.Ok(receipt);
        }
    }

    public class RefreshReceiptFromApiUseCase(IUnitOfWork unitOfWork, IReceiptAccessVerificationService accessVerification, IReceiptRefreshWorkflow receiptRefreshWorkflow) : IRefreshReceiptFromApiUseCase
    {
        public async Task<ServiceResult<Receipt>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
        {
            if (receiptId == Guid.Empty)
                return ServiceResult<Receipt>.Fail(400, "Некорректный идентификатор чека.");

            Receipt? receipt = await unitOfWork.Receipt.GetByIdWithItemsAndAccountsAsync(receiptId, asNoTracking: false, ct: ct);

            if (receipt == null)
                return ServiceResult<Receipt>.Fail(404, "Чек не найден.");

            if (!accessVerification.UserHasAccessToReceipt(currentUser, receipt))
                return ServiceResult<Receipt>.Fail(401, "Нет доступа к этому чеку.");

            return await receiptRefreshWorkflow.RefreshAsync(receipt, ct);
        }
    }

    public class RefreshReceiptsWithoutItemsUseCase(IUnitOfWork unitOfWork, IReceiptRefreshWorkflow receiptRefreshWorkflow, ILogger<RefreshReceiptsWithoutItemsUseCase> logger) : IRefreshReceiptsWithoutItemsUseCase
    {
        public async Task ExecuteAsync(CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetWithoutItemsAsync(ct);

            int countUpdatedReceipts = 0;
            foreach (Receipt receipt in receipts)
            {
                await Task.Delay(1000, ct);
                ServiceResult<Receipt> result = await receiptRefreshWorkflow.RefreshAsync(receipt, ct);
                if (result.Success)
                    countUpdatedReceipts++;
            }

            if (countUpdatedReceipts > 0)
                logger.LogInformation("[Method:{MethodName}] Refreshed {Count} receipts without items from external API.", nameof(ExecuteAsync), countUpdatedReceipts);
        }
    }

    public class DeleteReceiptUseCase(IUnitOfWork unitOfWork) : IDeleteReceiptUseCase
    {
        public async Task<ServiceResult<bool>> ExecuteAsync(Guid receiptId, User currentUser, CancellationToken ct)
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
    }

    public class ReceiptRefreshWorkflow(IUnitOfWork unitOfWork, IExternalReceiptProvider externalReceiptProvider) : IReceiptRefreshWorkflow
    {
        public async Task<ServiceResult<Receipt>> RefreshAsync(Receipt receipt, CancellationToken ct)
        {
            ServiceResult<Receipt> externalReceiptResult = await externalReceiptProvider.GetReceiptAsync(receipt, ct);
            if (!externalReceiptResult.Success || externalReceiptResult.Data == null)
                return ServiceResult<Receipt>.Fail(externalReceiptResult.Error!.StatusCode, externalReceiptResult.Error.Message);

            receipt.ApplyDetailsFrom(externalReceiptResult.Data);
            receipt.MarkUpdated(DateTime.UtcNow);
            receipt.Items.Clear();

            Dictionary<string, Product> productCache = new(StringComparer.Ordinal);
            foreach (ReceiptItem sourceItem in externalReceiptResult.Data.Items)
            {
                Product product = await ResolveProductAsync(sourceItem.Product, productCache, ct);
                ReceiptItem item = new()
                {
                    ReceiptId = receipt.Id,
                    ProductId = product.Id,
                    CategoryId = sourceItem.CategoryId,
                    Price = sourceItem.Price,
                    Sum = sourceItem.Sum,
                    Quantity = sourceItem.Quantity,
                    Nds = sourceItem.Nds,
                    PaymentType = sourceItem.PaymentType,
                    ProductType = sourceItem.ProductType,
                    ItemsQuantityMeasure = sourceItem.ItemsQuantityMeasure,
                    Product = product
                };

                receipt.Items.Add(item);
            }

            await unitOfWork.SaveChangesAsync(ct);
            return ServiceResult<Receipt>.Ok(receipt);
        }

        public async Task<Receipt> TryRefreshCreatedReceiptAsync(Receipt receipt, CancellationToken ct)
        {
            try
            {
                ServiceResult<Receipt> refreshResult = await RefreshAsync(receipt, ct);
                return refreshResult.Success && refreshResult.Data != null ? refreshResult.Data : receipt;
            }
            catch
            {
                return receipt;
            }
        }

        public async Task TryRefreshCreatedReceiptsAsync(IEnumerable<Receipt> receipts, CancellationToken ct)
        {
            foreach (Receipt receipt in receipts)
                await TryRefreshCreatedReceiptAsync(receipt, ct);
        }

        private async Task<Product> ResolveProductAsync(Product? sourceProduct, IDictionary<string, Product> productCache, CancellationToken ct)
        {
            if (sourceProduct == null)
                throw new InvalidOperationException("Внешний источник вернул позицию чека без товара.");

            string normalizedName = sourceProduct.NormalizedName;
            if (productCache.TryGetValue(normalizedName, out Product? cached))
            {
                cached.UpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName, sourceProduct.ProductCode);
                return cached;
            }

            Product? productFromDb = await unitOfWork.Product.GetItemByPredicateAsync(p => normalizedName == p.NormalizedName, ct: ct);
            if (productFromDb == null)
            {
                Product createdProduct = new();
                createdProduct.UpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName, sourceProduct.ProductCode);
                unitOfWork.Product.Create(createdProduct);
                productCache[normalizedName] = createdProduct;
                return createdProduct;
            }

            productFromDb.UpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName, sourceProduct.ProductCode);
            productCache[normalizedName] = productFromDb;
            return productFromDb;
        }
    }
}
