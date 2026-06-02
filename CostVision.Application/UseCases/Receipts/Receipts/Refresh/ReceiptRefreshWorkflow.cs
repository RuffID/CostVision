using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.UseCases.Receipts.Receipts.Refresh
{
    public class ReceiptRefreshWorkflow(IUnitOfWork unitOfWork, IExternalReceiptProvider externalReceiptProvider) : IReceiptRefreshWorkflow
    {
        public async Task<ServiceResult<Receipt>> RefreshAsync(Receipt receipt, CancellationToken ct)
        {
            ServiceResult<Receipt> externalReceiptResult = await externalReceiptProvider.GetReceiptAsync(receipt, ct);
            if (!externalReceiptResult.Success || externalReceiptResult.Data == null)
                return ServiceResult<Receipt>.Fail(externalReceiptResult.Error!.StatusCode, externalReceiptResult.Error.Message);

            Store? store = await ResolveStoreAsync(externalReceiptResult.Data.Store, ct);

            receipt.ApplyDetailsFrom(externalReceiptResult.Data);
            receipt.StoreId = store?.Id == Guid.Empty ? null : store?.Id;
            receipt.Store = store;
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
                cached.UpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName);
                return cached;
            }

            Product? productFromDb = await unitOfWork.Product.GetItemByPredicateAsync(p => normalizedName == p.NormalizedName, ct: ct);
            if (productFromDb == null)
            {
                Product createdProduct = new();
                createdProduct.UpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName);
                unitOfWork.Product.Create(createdProduct);
                productCache[normalizedName] = createdProduct;
                return createdProduct;
            }

            productFromDb.UpdateDetails(sourceProduct.Name, sourceProduct.NormalizedName);
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
                Store createdStore = new();
                createdStore.UpdateDetails(sourceStore.Name, sourceStore.NormalizedName, sourceStore.Address, sourceStore.NormalizedAddress);
                unitOfWork.Store.Create(createdStore);
                return createdStore;
            }

            storeFromDb.UpdateDetails(sourceStore.Name, sourceStore.NormalizedName, sourceStore.Address, sourceStore.NormalizedAddress);
            return storeFromDb;
        }
    }
}
