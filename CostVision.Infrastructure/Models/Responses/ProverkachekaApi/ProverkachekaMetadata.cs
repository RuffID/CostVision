namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaMetadata
    {
        // ID записи
        public long Id { get; set; }

        // ID ОФД
        public string? OfdId { get; set; }

        // Адрес торговой точки (как у ОФД)
        public string? Address { get; set; }

        // Тип документа
        public string? Subtype { get; set; }

        // Момент получения ОФД
        public DateTime ReceiveDate { get; set; }
    }
}
