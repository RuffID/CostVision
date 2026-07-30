using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.Abstractions.Api;
using CostVision.Infrastructure.Models.ConfigClass;
using CostVision.Infrastructure.Models.Requests.ProverkachekaApi;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;
using CostVision.Application.Models.Responses.Results;
using CostVision.Infrastructure.Services.Helpers;
using CostVision.Infrastructure.Services.Converters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace CostVision.Infrastructure.Services.Receipts
{
    public class ExternalReceiptProvider(IReceiptRequest receiptRequest, IOptions<ApiEndpointOptions> endpointOptions, IOptions<ProverkachekaOptions> apiOptions, ILogger<ExternalReceiptProvider> logger) : IExternalReceiptProvider
    {
        private readonly ApiEndpointOptions _endpointOptions = endpointOptions.Value;
        private readonly ProverkachekaOptions _apiOptions = apiOptions.Value;

        public async Task<ServiceResult<Receipt>> GetReceiptAsync(Receipt receipt, CancellationToken ct)
        {
            ProverkachekaManualRequest apiRequest = new()
            {
                ApiToken = _apiOptions.ProverkachekaApiToken,
                Fd = receipt.FiscalDocumentNumber,
                Fn = receipt.FiscalDriveNumber,
                Fp = receipt.FiscalSign,
                Time = receipt.DateTime.ToString("yyyyMMddTHHmm"),
                Summ = receipt.TotalSum.ToString(CultureInfo.InvariantCulture),
                OperationType = (int)receipt.OperationType
            };

            ProverkachekaResponse? result = await receiptRequest.GetReceiptByReceiptAsync(_endpointOptions.ProverkachekaApiUrl, apiRequest, ct);
            if (result == null)
                return ServiceResult<Receipt>.Fail(500, "Не удалось обновить чек по внешнему API.\nОшибка при получении ответа.");

            if (result.Code == (int)ReceiptResponseCodeEnum.Incorrect)
            {
                logger.LogWarning("[Method:{MethodName}] Receipt with Id {ReceiptId} could not be refreshed from API because it is marked as incorrect.", nameof(GetReceiptAsync), receipt.Id);
                return ServiceResult<Receipt>.Fail(500, $"Не удалось обновить чек по внешнему API.\nОшибка: {result.Data?.Error}");
            }

            if (result.Code == (int)ReceiptResponseCodeEnum.WaitBeforeRetry ||
                result.Code == (int)ReceiptResponseCodeEnum.RateLimitExceeded ||
                result.Code == (int)ReceiptResponseCodeEnum.Pending ||
                result.Code == (int)ReceiptResponseCodeEnum.Other)
            {
                return ServiceResult<Receipt>.Fail(500, $"Не удалось обновить чек по внешнему API.\nОшибка: {result.Data?.Error}");
            }

            if (result.Code != (int)ReceiptResponseCodeEnum.Correct || result.Data?.Json == null)
                return ServiceResult<Receipt>.Fail(500, $"Не удалось обновить чек по внешнему API.\nОшибка: {result.Data?.Error ?? "В ответе API отсутствует JSON чека."}");

            Receipt mappedReceipt;
            try
            {
                mappedReceipt = result.MapToReceipt(receipt.CreatedByUserId, receipt.CreatedAtUtc);
            }
            catch (InvalidOperationException exception)
            {
                return ServiceResult<Receipt>.Fail(500, $"Внешний API вернул некорректные данные чека: {exception.Message}");
            }

            mappedReceipt.Id = receipt.Id;
            if (!TryCreateStore(result.Data.Json.RetailPlace, result.Data.Json.RetailPlaceAddress, out Store? store, out string? storeError))
                return ServiceResult<Receipt>.Fail(500, $"Внешний API вернул некорректные данные магазина: {storeError}");

            mappedReceipt.AssignStore(store);
            foreach (ProverkachekaItem item in result.Data.Json.Items)
            {
                string normalizedName = NameNormalizedHelper.GetNormalizedName(item.Name);
                if (!Product.TryCreate(item.Name, normalizedName, out Product? product, out string? productError))
                {
                    return ServiceResult<Receipt>.Fail(
                        500,
                        $"Внешний API вернул некорректные данные товара: {productError}");
                }

                ReceiptItem receiptItem;
                try
                {
                    receiptItem = item.MapToReceiptItem(product!);
                }
                catch (InvalidOperationException exception)
                {
                    return ServiceResult<Receipt>.Fail(500, $"Внешний API вернул некорректную позицию чека: {exception.Message}");
                }

                if (!mappedReceipt.TryAddItem(receiptItem, out string? itemError))
                    return ServiceResult<Receipt>.Fail(500, $"Внешний API вернул некорректную позицию чека: {itemError}");
            }

            return ServiceResult<Receipt>.Ok(mappedReceipt);
        }

        private static bool TryCreateStore(
            string? retailPlace,
            string? retailPlaceAddress,
            out Store? store,
            out string? error)
        {
            if (string.IsNullOrWhiteSpace(retailPlace) && string.IsNullOrWhiteSpace(retailPlaceAddress))
            {
                store = null;
                error = null;
                return true;
            }

            string normalizedName = string.IsNullOrWhiteSpace(retailPlace)
                ? string.Empty
                : NameNormalizedHelper.GetNormalizedName(retailPlace);
            string normalizedAddress = string.IsNullOrWhiteSpace(retailPlaceAddress)
                ? string.Empty
                : NameNormalizedHelper.GetNormalizedName(retailPlaceAddress);

            return Store.TryCreate(
                retailPlace,
                normalizedName,
                retailPlaceAddress,
                normalizedAddress,
                out store,
                out error);
        }
    }
}
