using CostVision.Infrastructure.Services.Converters;
using Newtonsoft.Json;

namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    [JsonConverter(typeof(ProverkachekaDataConverter))]
    public class ProverkachekaData
    {
        public ProverkachekaJson? Json { get; set; }

        public string? Html { get; set; }

        /// <summary>
        /// В Error записывается ошибка, в случае когда "Data" приходит не объектом, а строкой
        /// </summary>
        public string? Error { get; set; }

        public bool HasError => !string.IsNullOrEmpty(Error);
    }
}
