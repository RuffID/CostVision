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
                Summ = receipt.TotalSum.ToString(),
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

            Receipt mappedReceipt = result.MapToReceipt();
            mappedReceipt.Items = result.Data?.Json?.Items
                .Select(item =>
                {
                    string normalizedName = NameNormalizedHelper.GetNormalizedName(item.Name);
                    Product product = new();
                    product.UpdateDetails(item.Name, normalizedName, item.ProductCode?.RawProductCode);

                    ReceiptItem receiptItem = item.MapToReceiptItem(receipt.Id, Guid.Empty);
                    receiptItem.Product = product;

                    return receiptItem;
                })
                .ToList() ?? new List<ReceiptItem>();

            return ServiceResult<Receipt>.Ok(mappedReceipt);
        }
    }
}
