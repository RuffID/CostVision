namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaGs1m
    {
        // GTIN
        public string? Gtin { get; set; }

        // Серийный номер
        public string? SerialNumber { get; set; }

        // Тип идентификатора
        public int ProductIdType { get; set; }

        // Сырой код продукции
        public string? RawProductCode { get; set; }
    }
}
