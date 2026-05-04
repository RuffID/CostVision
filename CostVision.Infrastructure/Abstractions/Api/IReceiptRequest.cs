using CostVision.Infrastructure.Models.Requests.ProverkachekaApi;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;
using Microsoft.AspNetCore.Http;

namespace CostVision.Infrastructure.Abstractions.Api
{
    public interface IReceiptRequest
    {
        Task<ProverkachekaResponse?> GetReceiptByQrRawAsync(string url, string apiToken, string qrRaw, CancellationToken ct = default);

        Task<ProverkachekaResponse?> GetReceiptByReceiptAsync(string url, ProverkachekaManualRequest requestDto, CancellationToken ct = default);

        Task<ProverkachekaResponse?> GetReceiptByFileAsync(string url, string apiToken, IFormFile file, CancellationToken ct = default);
    }
}