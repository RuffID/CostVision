using System.Text.Json.Serialization;

namespace CostVision.Infrastructure.Models.Requests.ProverkachekaApi
{
    public class ProverkachekaManualRequest
    {
        /// <summary>
        /// Токен доступа
        /// </summary>
        [JsonPropertyName("token")]
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Номер ФН (фискального накопителя)
        /// </summary>
        [JsonPropertyName("fn")]
        public string Fn { get; set; } = string.Empty;

        /// <summary>
        /// Номер ФД (фискального документа)
        /// </summary>
        [JsonPropertyName("fd")]
        public string Fd { get; set; } = string.Empty;

        /// <summary>
        /// Признак фискального документа ФПД (ФП)
        /// </summary>
        [JsonPropertyName("fp")]
        public string Fp { get; set; } = string.Empty;

        /// <summary>
        /// Дата/время в формате t=20251112T1443
        /// </summary>
        [JsonPropertyName("t")]
        public string Time { get; set; } = string.Empty;

        /// <summary>
        /// Тип операции (1..4)
        /// </summary>
        [JsonPropertyName("n")]
        public int OperationType { get; set; }

        /// <summary>
        /// Сумма чека в рублях с точкой
        /// </summary>
        [JsonPropertyName("s")]
        public string Summ { get; set; } = string.Empty;

        /// <summary>
        /// Признак сканирования QR (0/1)
        /// </summary>
        [JsonPropertyName("qr")]
        public int? QrFlag { get; set; }
    }
}
