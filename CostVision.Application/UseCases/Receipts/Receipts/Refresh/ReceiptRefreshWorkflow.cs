using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts.Refresh
{
    public class ReceiptRefreshWorkflow(
        IUnitOfWork unitOfWork,
        IExternalReceiptProvider externalReceiptProvider,
        TimeProvider timeProvider) : IReceiptRefreshWorkflow
    {
        public async Task<ServiceResult<Receipt>> RefreshAsync(Receipt receipt, CancellationToken ct)
        {
            DateTime attemptedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            ServiceResult<Receipt> result = await ApplyRefreshAsync(receipt, attemptedAtUtc, ct);
            if (!result.Success)
                return result;

            if (!receipt.TryCompleteRefresh(attemptedAtUtc, registerAttempt: false, out string? completionError))
                throw new InvalidOperationException(completionError);

            await unitOfWork.SaveChangesAsync(ct);
            return result;
        }

        public async Task<ServiceResult<Receipt>> RefreshScheduledAsync(
            Receipt receipt,
            DateTime attemptedAtUtc,
            DateTime nextAttemptAtUtc,
            CancellationToken ct)
        {
            if (!receipt.CanAttemptRefresh(attemptedAtUtc, ReceiptRefreshPolicy.MAX_ATTEMPTS))
                throw new InvalidOperationException("Фоновая попытка обновления чека запущена вне расписания.");

            if (nextAttemptAtUtc <= attemptedAtUtc)
                throw new InvalidOperationException("Следующая фоновая попытка должна быть запланирована позже текущей.");

            ServiceResult<Receipt> result = await ApplyRefreshAsync(receipt, attemptedAtUtc, ct);
            if (!result.Success)
            {
                if (!receipt.TryRegisterRefreshFailure(
                        result.Error.Message,
                        attemptedAtUtc,
                        nextAttemptAtUtc,
                        ReceiptRefreshPolicy.MAX_ATTEMPTS,
                        out string? failureError))
                    throw new InvalidOperationException(failureError);

                await unitOfWork.SaveChangesAsync(ct);
                return result;
            }

            if (!receipt.TryCompleteRefresh(attemptedAtUtc, registerAttempt: true, out string? completionError))
                throw new InvalidOperationException(completionError);

            await unitOfWork.SaveChangesAsync(ct);
            return result;
        }

        private async Task<ServiceResult<Receipt>> ApplyRefreshAsync(Receipt receipt, DateTime updatedAtUtc, CancellationToken ct)
        {
            ServiceResult<Receipt> externalReceiptResult = await externalReceiptProvider.GetReceiptAsync(receipt, ct);
            if (!externalReceiptResult.Success)
                return externalReceiptResult.PropagateFailure<Receipt>();

            Store? store = await ResolveStoreAsync(externalReceiptResult.Data.Store, ct);

            Dictionary<string, Product> productCache = new(StringComparer.Ordinal);
            List<ReceiptItem> refreshedItems = new();
            foreach (ReceiptItem sourceItem in externalReceiptResult.Data.Items)
            {
                Product product = await ResolveProductAsync(sourceItem.Product, productCache, ct);
                if (!ReceiptItem.TryCreate(
                        sourceItem.Price,
                        sourceItem.Quantity,
                        sourceItem.Sum,
                        sourceItem.Nds,
                        sourceItem.PaymentType,
                        sourceItem.ProductType,
                        sourceItem.ItemsQuantityMeasure,
                        product,
                        sourceItem.CategoryId,
                        out ReceiptItem? item,
                        out string? itemError))
                    return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, $"Внешний источник вернул некорректную позицию чека: {itemError}");

                refreshedItems.Add(item!);
            }

            if (!receipt.TryRefreshFrom(externalReceiptResult.Data, store, refreshedItems, updatedAtUtc, out string? refreshError))
                return ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, $"Внешний источник вернул некорректные данные чека: {refreshError}");

            return ServiceResult<Receipt>.Ok(receipt);
        }

        private async Task<Product> ResolveProductAsync(Product? sourceProduct, IDictionary<string, Product> productCache, CancellationToken ct)
        {
            if (sourceProduct == null)
                throw new InvalidOperationException("Внешний источник вернул позицию чека без товара.");

            string normalizedName = sourceProduct.NormalizedName;
            if (productCache.TryGetValue(normalizedName, out Product? cached))
            {
                if (!cached.TryUpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName, out string? error))
                    throw new InvalidOperationException($"Не удалось обновить данные товара: {error}");

                return cached;
            }

            Product? productFromDb = await unitOfWork.Product.GetItemByPredicateAsync(p => normalizedName == p.NormalizedName, ct: ct);
            if (productFromDb == null)
            {
                if (!Product.TryCreate(sourceProduct.Name, sourceProduct.NormalizedName, out Product? createdProduct, out string? error))
                    throw new InvalidOperationException($"Не удалось создать товар: {error}");

                Product newProduct = createdProduct!;
                unitOfWork.Product.Create(newProduct);
                productCache[normalizedName] = newProduct;
                return newProduct;
            }

            if (!productFromDb.TryUpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName, out string? updateError))
                throw new InvalidOperationException($"Не удалось обновить данные товара: {updateError}");

            productCache[normalizedName] = productFromDb;
            return productFromDb;
        }

        private async Task<Store?> ResolveStoreAsync(Store? sourceStore, CancellationToken ct)
        {
            if (sourceStore == null)
                return null;

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
                    throw new InvalidOperationException($"Не удалось создать магазин: {error}");

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
                throw new InvalidOperationException($"Не удалось обновить данные магазина: {updateError}");

            return storeFromDb;
        }
    }
}
