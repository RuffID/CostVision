using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;
using CostVision.Application.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CostVision.Application.UseCases.Receipts.Receipts.Refresh
{
    public class ReceiptRefreshWorkflow(
        IReceiptRefreshUnitOfWork unitOfWork,
        IExternalReceiptProvider externalReceiptProvider,
        TimeProvider timeProvider) : IReceiptRefreshWorkflow
    {
        public async Task<ServiceResult<Receipt>> RefreshAsync(Receipt receipt, CancellationToken ct, Guid currentUserId)
        {
            if (currentUserId == Guid.Empty)
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.Validation, "Не указан пользователь обновления чека.");
            }

            DateTime attemptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            return await ExecuteAsync(receipt, attemptedAtUtc, null, currentUserId, ct);
        }

        public async Task<ServiceResult<Receipt>> RefreshScheduledAsync(
            Receipt receipt,
            DateTime attemptedAtUtc,
            DateTime nextAttemptAtUtc,
            CancellationToken ct)
        {
            if (!receipt.CanAttemptRefresh(attemptedAtUtc, ReceiptRefreshPolicy.MAX_ATTEMPTS))
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.Conflict, "Чек больше не ожидает фоновую попытку.");
            }

            if (nextAttemptAtUtc <= attemptedAtUtc)
            {
                throw new InvalidOperationException("Следующая фоновая попытка должна быть запланирована позже текущей.");
            }

            return await ExecuteAsync(receipt, attemptedAtUtc, nextAttemptAtUtc, null, ct);
        }

        private async Task<ServiceResult<Receipt>> ExecuteAsync(
            Receipt snapshot, DateTime attemptedAtUtc, DateTime? nextAttemptAtUtc, Guid? currentUserId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            byte[] expectedVersion = snapshot.RowVersion.ToArray();
            ServiceResult<Receipt> prepared = await PrepareAsync(snapshot, attemptedAtUtc, ct);
            ct.ThrowIfCancellationRequested();
            if (!prepared.Success && !nextAttemptAtUtc.HasValue)
            {
                return prepared;
            }

            return await SavePreparedAsync(snapshot, expectedVersion, prepared, attemptedAtUtc, nextAttemptAtUtc, currentUserId, ct);
        }

        public async Task<ServiceResult<Receipt>> RegisterScheduledFailureAsync(
            Receipt snapshot, string error, DateTime attemptedAtUtc, DateTime nextAttemptAtUtc, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(error) || nextAttemptAtUtc <= attemptedAtUtc)
            {
                throw new InvalidOperationException("Некорректное состояние неудачной фоновой попытки.");
            }

            ServiceResult<Receipt> failure = ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, error);

            return await SavePreparedAsync(snapshot, snapshot.RowVersion.ToArray(), failure,
                attemptedAtUtc, nextAttemptAtUtc, null, ct);
        }

        private async Task<ServiceResult<Receipt>> SavePreparedAsync(
            Receipt snapshot, byte[] expectedVersion, ServiceResult<Receipt> prepared, DateTime attemptedAtUtc,
            DateTime? nextAttemptAtUtc, Guid? currentUserId, CancellationToken ct)
        {
            ServiceResult<Receipt> result = prepared;
            try
            {
                await unitOfWork.ExecuteInTransaction(async transactionCt =>
                {
                    Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
                        item => item.Id == snapshot.Id,
                        asNoTracking: false,
                        include: query => query
                            .Include(item => item.Store)
                            .Include(item => item.CreatedByUser)
                            .Include(item => item.Items).ThenInclude(item => item.Product)
                            .Include(item => item.Accounts).ThenInclude(link => link.Account)
                                .ThenInclude(account => account!.Members),
                        ct: transactionCt);
                    if (receipt == null)
                    {
                        result = ServiceResult<Receipt>.Fail(ServiceErrorType.NotFound, "Чек не найден.");
                        return;
                    }

                    if (currentUserId.HasValue && !FinancialDataAccess.CanReadReceipt(receipt, currentUserId.Value))
                    {
                        result = ServiceResult<Receipt>.Fail(ServiceErrorType.Forbidden, "Нет доступа к этому чеку.");
                        return;
                    }

                    bool staleVersion = !expectedVersion.SequenceEqual(receipt.RowVersion);
                    bool differentDocument = snapshot.GetIdentity() != receipt.GetIdentity();
                    bool noLongerPending = nextAttemptAtUtc.HasValue &&
                        (!receipt.CanAttemptRefresh(attemptedAtUtc, ReceiptRefreshPolicy.MAX_ATTEMPTS) || receipt.Items.Any());
                    if (staleVersion || differentDocument || noLongerPending)
                    {
                        result = ServiceResult<Receipt>.Fail(ServiceErrorType.Conflict,
                            "Чек изменён другим запросом. Обновите данные и повторите запрос.");
                        return;
                    }

                    transactionCt.ThrowIfCancellationRequested();
                    if (!prepared.Success)
                    {
                        if (!receipt.TryRegisterRefreshFailure(prepared.Error.Message, attemptedAtUtc,
                                nextAttemptAtUtc!.Value, ReceiptRefreshPolicy.MAX_ATTEMPTS, out string? failureError))
                        {
                            throw new InvalidOperationException(failureError);
                        }
                    }
                    else
                    {
                        await ApplyRefreshAsync(receipt, prepared.Data, attemptedAtUtc, transactionCt);
                        if (!receipt.TryCompleteRefresh(attemptedAtUtc, nextAttemptAtUtc.HasValue, out string? completionError))
                        {
                            throw new InvalidOperationException(completionError);
                        }

                        result = ServiceResult<Receipt>.Ok(receipt);
                    }

                    transactionCt.ThrowIfCancellationRequested();
                    await unitOfWork.SaveChangesAsync(transactionCt);
                }, IsolationLevel.Serializable, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.Conflict,
                    "Чек изменён другим запросом. Обновите данные и повторите запрос.");
            }
            catch (ReceiptWriteConflictException)
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.Conflict,
                    "Конкурентное обновление чека отклонено. Обновите данные и повторите запрос.");
            }

            return result;
        }

        private async Task<ServiceResult<Receipt>> PrepareAsync(Receipt receipt, DateTime updatedAtUtc, CancellationToken ct)
        {
            ServiceResult<Receipt> externalReceiptResult;
            try
            {
                externalReceiptResult = await externalReceiptProvider.GetReceiptAsync(receipt, ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "Истекло время ожидания внешнего API.");
            }
            catch (HttpRequestException)
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "Временная ошибка соединения с внешним API.");
            }
            catch (TimeoutException)
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "Истекло время ожидания внешнего API.");
            }

            if (!externalReceiptResult.Success)
            {
                return externalReceiptResult.PropagateFailure<Receipt>();
            }

            Receipt source = externalReceiptResult.Data;
            if (receipt.GetIdentity() != source.GetIdentity())
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService,
                    "Внешний источник вернул другой фискальный документ.");
            }

            if (!Receipt.TryCreate(source.FiscalDriveNumber, source.FiscalDocumentNumber, source.FiscalSign,
                    source.DateTime, source.OperationType, source.TotalSum, receipt.CreatedByUserId,
                    receipt.CreatedAtUtc, out Receipt? preview, out string? error))
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, error!);
            }

            Store? store = null;
            if (source.Store != null && !Store.TryCreate(source.Store.Name, source.Store.NormalizedName,
                    source.Store.Address, source.Store.NormalizedAddress, out store, out error))
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, error!);
            }

            List<ReceiptItem> items = new();
            foreach (ReceiptItem sourceItem in source.Items)
            {
                Product? product = sourceItem.Product;
                if (product == null || !Product.TryCreate(product.Name, product.NormalizedName, out Product? clone, out error))
                {
                    return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "Некорректный товар внешнего чека.");
                }

                if (!TryCopyItem(sourceItem, clone!, out ReceiptItem? item, out error))
                {
                    return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, error!);
                }

                items.Add(item!);
            }

            if (!preview!.TryRefreshFrom(source, store, items, updatedAtUtc, out error))
            {
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, error!);
            }

            return ServiceResult<Receipt>.Ok(preview);
        }

        private async Task ApplyRefreshAsync(Receipt receipt, Receipt source, DateTime updatedAtUtc, CancellationToken ct)
        {
            Store? store = await ResolveStoreAsync(source.Store, ct);

            Dictionary<string, Product> productCache = new(StringComparer.Ordinal);
            List<ReceiptItem> refreshedItems = new();
            foreach (ReceiptItem sourceItem in source.Items.OrderBy(item => item.Product!.NormalizedName, StringComparer.Ordinal))
            {
                await ResolveProductAsync(sourceItem.Product, productCache, ct);
            }

            foreach (ReceiptItem sourceItem in source.Items)
            {
                Product product = productCache[sourceItem.Product!.NormalizedName];
                if (!TryCopyItem(sourceItem, product, out ReceiptItem? item, out string? itemError))
                {
                    throw new InvalidOperationException(itemError);
                }

                refreshedItems.Add(item!);
            }

            if (!receipt.TryRefreshFrom(source, store, refreshedItems, updatedAtUtc, out string? refreshError))
            {
                throw new InvalidOperationException(refreshError);
            }
        }

        private static bool TryCopyItem(ReceiptItem source, Product product, out ReceiptItem? item, out string? error)
        {
            return ReceiptItem.TryCreate(source.Price, source.Quantity, source.Sum, source.Nds,
                source.PaymentType, source.ProductType, source.ItemsQuantityMeasure, product, source.CategoryId,
                out item, out error);
        }

        private async Task<Product> ResolveProductAsync(Product? sourceProduct, IDictionary<string, Product> productCache, CancellationToken ct)
        {
            if (sourceProduct == null)
            {
                throw new InvalidOperationException("Внешний источник вернул позицию чека без товара.");
            }

            string normalizedName = sourceProduct.NormalizedName;
            if (productCache.TryGetValue(normalizedName, out Product? cached))
            {
                if (!cached.TryUpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName, out string? error))
                {
                    throw new InvalidOperationException($"Не удалось обновить данные товара: {error}");
                }

                return cached;
            }

            Product? productFromDb = await unitOfWork.Product.GetItemByPredicateAsync(p => normalizedName == p.NormalizedName, ct: ct);
            if (productFromDb == null)
            {
                if (!Product.TryCreate(sourceProduct.Name, sourceProduct.NormalizedName, out Product? createdProduct, out string? error))
                {
                    throw new InvalidOperationException($"Не удалось создать товар: {error}");
                }

                Product newProduct = createdProduct!;
                unitOfWork.Product.Create(newProduct);
                productCache[normalizedName] = newProduct;

                return newProduct;
            }

            if (!productFromDb.TryUpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName, out string? updateError))
            {
                throw new InvalidOperationException($"Не удалось обновить данные товара: {updateError}");
            }

            productCache[normalizedName] = productFromDb;

            return productFromDb;
        }

        private async Task<Store?> ResolveStoreAsync(Store? sourceStore, CancellationToken ct)
        {
            if (sourceStore == null)
            {
                return null;
            }

            string normalizedName = sourceStore.NormalizedName;
            string normalizedAddress = sourceStore.NormalizedAddress;
            Store? storeFromDb = await unitOfWork.Store.GetItemByPredicateAsync(
                store => store.NormalizedName == normalizedName && store.NormalizedAddress == normalizedAddress,
                ct: ct);

            if (storeFromDb == null)
            {
                if (!Store.TryCreate(
                        sourceStore.Name,
                        sourceStore.NormalizedName,
                        sourceStore.Address,
                        sourceStore.NormalizedAddress,
                        out Store? createdStore,
                        out string? error))
                {
                    throw new InvalidOperationException($"Не удалось создать магазин: {error}");
                }

                Store newStore = createdStore!;
                unitOfWork.Store.Create(newStore);

                return newStore;
            }

            if (!storeFromDb.TryUpdateDetails(
                    sourceStore.Name,
                    sourceStore.NormalizedName,
                    sourceStore.Address,
                    sourceStore.NormalizedAddress,
                    out string? updateError))
            {
                throw new InvalidOperationException($"Не удалось обновить данные магазина: {updateError}");
            }

            return storeFromDb;
        }
    }
}
