using Newtonsoft.Json;

namespace CostVision.Models.Requests.ProverkachekaApi
{
    public class ProverkachekaManualRequest
    {
        // Токен доступа
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        // Номер ФН
        [JsonProperty("fn")]
        public string Fn { get; set; } = string.Empty;

        // Номер ФД
        [JsonProperty("fd")]
        public string Fd { get; set; } = string.Empty;

        // ФПД
        [JsonProperty("fp")]
        public string Fp { get; set; } = string.Empty;

        // Дата/время в формате t=20251112T1443
        [JsonProperty("t")]
        public string T { get; set; } = string.Empty;

        // Тип операции (1..4)
        [JsonProperty("n")]
        public int OperationType { get; set; }

        // Сумма чека в рублях с точкой
        [JsonProperty("s")]
        public string Sum { get; set; } = string.Empty;

        // Признак сканирования QR (0/1)
        [JsonProperty("qr")]
        public int? QrFlag { get; set; }
    }
}
