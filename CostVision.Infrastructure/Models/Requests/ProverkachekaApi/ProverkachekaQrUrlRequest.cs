using System.Text.Json.Serialization;

namespace CostVision.Infrastructure.Models.Requests.ProverkachekaApi
{
    public class ProverkachekaQrUrlRequest
    {
        // Токен доступа
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        // Ссылка на картинку с QR-кодом
        [JsonPropertyName("qrurl")]
        public string QrUrl { get; set; } = string.Empty;
    }
}