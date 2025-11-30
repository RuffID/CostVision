using Newtonsoft.Json;

namespace CostVision.Models.Requests.ProverkachekaApi
{
    public class ProverkachekaQrUrlRequest
    {
        // Токен доступа
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        // Ссылка на картинку с QR-кодом
        [JsonProperty("qrurl")]
        public string QrUrl { get; set; } = string.Empty;
    }
}