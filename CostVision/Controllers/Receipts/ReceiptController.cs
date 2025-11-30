using Microsoft.AspNetCore.Mvc;

namespace CostVision.Controllers.Receipts
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReceiptController(/*IUnitOfWork unitOfWork, ReceiptInfoRequest receiptRequest, IOptions<ProverkachekaOptions> proverkachekaOptions, 
        IOptions<ApiEndpointOptions> endpoint,*/ ILoggerFactory logger) : Controller
    {
        private readonly ILogger<ReceiptController> _logger = logger.CreateLogger<ReceiptController>();

        /*[HttpPost]
        public async Task<IActionResult> UpdateReceiptFromOFD(CancellationToken ct)
        {
            List<Receipt> receipts = await unitOfWork.Receipt.GetItemsByPredicate(r => r.Items.Count == 0, asNoTracking: false, ct: ct);

            if (receipts.Count == 0)
                return Ok("No receipts found to update.");

            int updatedEntities = 0;
            Dictionary<string, Product> productsCache = new(StringComparer.OrdinalIgnoreCase);

            foreach (Receipt receipt in receipts)
            {
                ProverkachekaManualRequest requestDto = new()
                {
                    Token = proverkachekaOptions.Value.ProverkachekaApiToken,
                    Fn = receipt.FiscalDriveNumber,
                    Fd = receipt.FiscalDocumentNumber,
                    Fp = receipt.FiscalSign,
                    T = receipt.DateTime.ToString("yyyy-MM-ddTHH:mm"),
                    OperationType = (int)receipt.OperationType,
                    Sum = receipt.TotalSum.ToString()
                };

                ProverkachekaResponse? response;
                try
                {
                    response = await receiptRequest.GetReceiptAsync(endpoint.Value.ProverkachekaApiUrl, proverkachekaOptions.Value.ProverkachekaApiToken, requestDto, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error parsing receiptId: {ReceiptId}", receipt.Id);
                    continue;
                }

                if (response == null)
                {
                    _logger.LogWarning("Received empty receipt from API.");
                    continue;
                }

                receipt.CopyData(response.MapToReceipt());
                updatedEntities++;

                if (response.Data?.Json == null)
                    continue;


                foreach (ProverkachekaItem item in response.Data.Json.Items)
                {
                    string name = item.Name.Trim();
                    string normalizedName = NameNormalizedHelper.NormalizeProductName(name);

                    if (!productsCache.TryGetValue(normalizedName, out Product? product))
                    {
                        product = await unitOfWork.Product.GetItemByPredicate(p => p.NormalizedName == normalizedName, asNoTracking: false, ct: ct);

                        if (product == null)
                        {
                            product = new()
                            {
                                Name = name,
                                NormalizedName = normalizedName
                            };

                            unitOfWork.Product.Create(product);
                        }

                        productsCache[normalizedName] = product;
                    }

                    receipt.Items.Add(item.MapToReceiptItem(receipt, product));
                }
            }

            await unitOfWork.SaveAsync(ct);
            return Ok();
        }

        [HttpPost("manual-request")]
        public async Task<IActionResult> CreateReceiptFromManualRequest([FromBody] ProverkachekaManualRequest requestDto, [FromQuery] Guid userId, CancellationToken ct = default)
        {
            // паттерны под строку qrRaw
            string qrRaw = $"t={requestDto.T}&s={requestDto.Sum}&fn={requestDto.Fn}&i={requestDto.Fd}&fp={requestDto.Fp}&n={requestDto.OperationType}";

            Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicate(r =>
                r.CreatedByUserId == userId && (
                (r.FiscalDriveNumber == requestDto.Fn &&
                 r.FiscalDocumentNumber == requestDto.Fd &&
                 r.FiscalSign == requestDto.Fp)
                ),
            asNoTracking: true,
            ct: ct);

            if (receipt != null)
                return Conflict("Receipt already exists.");

            ProverkachekaResponse? response = null;
            try
            {
                response = await receiptRequest.GetReceiptAsync(endpoint.Value.ProverkachekaApiUrl, proverkachekaOptions.Value.ProverkachekaApiToken, requestDto, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing receipt. Fn: {ReceiptFn}", requestDto.Fn);
            }

            if (response == null)
            {
                _logger.LogWarning("Received empty receipt from API, fn: {FiscalDriveNumber}, fd: {FiscalDocumentNumber}, fp: {FiscalSign}.", requestDto.Fn, requestDto.Fn, requestDto.Fp);
                return NotFound("Receipt not found in Api.");
            }
            if (response.Code != 1)
            {
                string message = response.Data?.Error ?? $"API вернул код {response.Code}.";
                return NotFound(message);
            }

            if (response.Data == null || response.Data.Json == null)
            {
                string message = response.Data?.Error ?? "В ответе отсутствует JSON с чеком.";
                return NotFound(message);
            }

            receipt = response.MapToReceipt();
            receipt.CreatedByUserId = userId;
            receipt.CreatedAtUtc = DateTime.UtcNow;
            unitOfWork.Receipt.Create(receipt);

            if (response.Data?.Json != null)
            {
                Dictionary<string, Product> productsCache = new(StringComparer.OrdinalIgnoreCase);
                foreach (ProverkachekaItem item in response.Data.Json.Items)
                {
                    string name = item.Name.Trim();
                    string normalizedName = NameNormalizedHelper.NormalizeProductName(name);

                    if (!productsCache.TryGetValue(normalizedName, out Product? product))
                    {
                        product = await unitOfWork.Product.GetItemByPredicate(p => p.NormalizedName == normalizedName, asNoTracking: false, ct: ct);

                        if (product == null)
                        {
                            product = new()
                            {
                                Name = name,
                                NormalizedName = normalizedName
                            };

                            unitOfWork.Product.Create(product);
                        }

                        productsCache[normalizedName] = product;
                    }

                    receipt.Items.Add(item.MapToReceiptItem(receipt, product));
                }
            }

            await unitOfWork.SaveAsync(ct);
            return Ok();
        }*/
    }
}
