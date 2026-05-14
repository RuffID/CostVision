using System.Text.Json.Serialization;

namespace CostVision.Infrastructure.Models.Requests.ProverkachekaApi
{
    public class ProverkachekaQrRawRequest
    {
        // Токен доступа
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        // Сырая строка QR-кода
        [JsonPropertyName("qrraw")]
        public string QrRaw { get; set; } = string.Empty;

        // Идентификатор акции (если нужно)
        [JsonPropertyName("promoid")]
        public int? PromoId { get; set; }
    }
}
