using Newtonsoft.Json;

namespace CostVision.Infrastructure.Models.Requests.ProverkachekaApi
{
    public class ProverkachekaManualRequest
    {
        /// <summary>
        /// Токен доступа
        /// </summary>
        [JsonProperty("token")]
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Номер ФН (фискального накопителя)
        /// </summary>
        [JsonProperty("fn")]
        public string Fn { get; set; } = string.Empty;

        /// <summary>
        /// Номер ФД (фискального документа)
        /// </summary>
        [JsonProperty("fd")]
        public string Fd { get; set; } = string.Empty;

        /// <summary>
        /// Признак фискального документа ФПД (ФП)
        /// </summary>
        [JsonProperty("fp")]
        public string Fp { get; set; } = string.Empty;

        /// <summary>
        /// Дата/время в формате t=20251112T1443
        /// </summary>
        [JsonProperty("t")]
        public string Time { get; set; } = string.Empty;

        /// <summary>
        /// Тип операции (1..4)
        /// </summary>
        [JsonProperty("n")]
        public int OperationType { get; set; }

        /// <summary>
        /// Сумма чека в рублях с точкой
        /// </summary>
        [JsonProperty("s")]
        public string Summ { get; set; } = string.Empty;

        /// <summary>
        /// Признак сканирования QR (0/1)
        /// </summary>
        [JsonProperty("qr")]
        public int? QrFlag { get; set; }
    }
}
