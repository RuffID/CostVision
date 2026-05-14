using CostVision.Infrastructure.Services.Converters;
using System.Text.Json.Serialization;

namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    [JsonConverter(typeof(ProverkachekaDataConverter))]
    public class ProverkachekaData
    {
        public ProverkachekaJson? Json { get; set; }

        public string? Html { get; set; }

        /// <summary>
        /// Содержит текст ошибки, если поле data пришло строкой вместо объекта.
        /// </summary>
        public string? Error { get; set; }

        public bool HasError => !string.IsNullOrEmpty(Error);
    }
}
