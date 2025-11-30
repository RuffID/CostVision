using CostVision.Models.Requests.ProverkachekaApi;
using CostVision.Models.Responses.ProverkachekaApi;
using HttpApiClientLibrary.Interfaces;
using System.Net.Http.Headers;

namespace CostVision.Services.Api
{
    public class ReceiptInfoRequest(IHttpApiClient httpApiClient)
    {
        public async Task<ProverkachekaResponse?> GetReceiptAsync(string url, string apiToken, string qrRaw, CancellationToken ct = default)
        {
            ProverkachekaQrRawRequest requestDto = new ()
            {
                Token = apiToken,
                QrRaw = qrRaw
            };

            return await httpApiClient.PostAsync<ProverkachekaQrRawRequest, ProverkachekaResponse>(url, requestDto, ct: ct);
        }

        public async Task<ProverkachekaResponse?> GetReceiptAsync(string url, string apiToken, ProverkachekaManualRequest requestDto, CancellationToken ct = default)
        {
            requestDto.Token = apiToken;

            return await httpApiClient.PostAsync<ProverkachekaManualRequest, ProverkachekaResponse>(url, requestDto, ct: ct);
        }

        public async Task<ProverkachekaResponse?> GetReceiptAsync(string url, string apiToken, IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty.", nameof(file));

            using MultipartFormDataContent form = new ();

            await using Stream fileStream = file.OpenReadStream();
            StreamContent fileContent = new (fileStream);

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "image/jpeg");

            form.Add(fileContent, "qrfile", file.FileName);
            form.Add(new StringContent(apiToken), "token");

            ProverkachekaResponse? result = await httpApiClient.PostAsync<HttpContent, ProverkachekaResponse>(url, form, ct: ct);

            return result;
        }
    }
}