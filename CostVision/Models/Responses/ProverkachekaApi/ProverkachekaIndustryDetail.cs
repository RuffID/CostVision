namespace CostVision.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaIndustryDetail
    {
        // ID ФОИВ
        public string? IdFoiv { get; set; }

        // Значение отраслевого параметра
        public string? IndustryPropValue { get; set; }

        // Номер документа-основания
        public string? FoundationDocNumber { get; set; }

        // Дата документа-основания
        public string? FoundationDocDateTime { get; set; }
    }
}
