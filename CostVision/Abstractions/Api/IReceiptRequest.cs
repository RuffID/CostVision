using CostVision.Models.Requests.ProverkachekaApi;
using CostVision.Models.Responses.ProverkachekaApi;

namespace CostVision.Abstractions.Api
{
    public interface IReceiptRequest
    {
        Task<ProverkachekaResponse?> GetReceiptByQrRawAsync(string url, string apiToken, string qrRaw, CancellationToken ct = default);
        Task<ProverkachekaResponse?> GetReceiptByReceiptAsync(string url, ProverkachekaManualRequest requestDto, CancellationToken ct = default);
        Task<ProverkachekaResponse?> GetReceiptByFileAsync(string url, string apiToken, IFormFile file, CancellationToken ct = default);
    }
}
