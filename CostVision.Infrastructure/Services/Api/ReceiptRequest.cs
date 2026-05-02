using CostVision.Infrastructure.Abstractions.Api;
using CostVision.Infrastructure.Models.Requests.ProverkachekaApi;
using CostVision.Infrastructure.Models.Responses.ProverkachekaApi;
using HttpClientLibrary.Abstractions;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace CostVision.Infrastructure.Services.Api
{
    public class ReceiptRequest(IHttpApiClient httpApiClient) : IReceiptRequest
    {
        public async Task<ProverkachekaResponse?> GetReceiptByQrRawAsync(string url, string apiToken, string qrRaw, CancellationToken ct = default)
        {
            ProverkachekaQrRawRequest requestDto = new()
            {
                Token = apiToken,
                QrRaw = qrRaw
            };

            return await httpApiClient.PostAsync<ProverkachekaQrRawRequest, ProverkachekaResponse>(url, requestDto, ct: ct);
        }

        public async Task<ProverkachekaResponse?> GetReceiptByReceiptAsync(string url, ProverkachekaManualRequest requestDto, CancellationToken ct = default)
        {
            return await httpApiClient.PostAsync<ProverkachekaManualRequest, ProverkachekaResponse>(url, requestDto, ct: ct);
        }

        public async Task<ProverkachekaResponse?> GetReceiptByFileAsync(string url, string apiToken, IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.", nameof(file));

            using MultipartFormDataContent form = new();

            await using Stream fileStream = file.OpenReadStream();
            StreamContent fileContent = new(fileStream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "image/jpeg");

            form.Add(fileContent, "qrfile", file.FileName);
            form.Add(new StringContent(apiToken), "token");

            return await httpApiClient.PostAsync<HttpContent, ProverkachekaResponse>(url, form, ct: ct);
        }
    }
}