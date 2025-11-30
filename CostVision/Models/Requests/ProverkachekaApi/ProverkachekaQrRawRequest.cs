using Newtonsoft.Json;

namespace CostVision.Models.Requests.ProverkachekaApi
{
    public class ProverkachekaQrRawRequest
    {
        // Токен доступа
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        // Сырая строка QR-кода
        [JsonProperty("qrraw")]
        public string QrRaw { get; set; } = string.Empty;

        // Идентификатор акции (если нужно)
        [JsonProperty("promoid")]
        public int? PromoId { get; set; }
    }
}
