namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaProductCode
    {
        public string Gtin { get; set; } = string.Empty;
        public string Sernum { get; set; } = string.Empty;
        public int ProductIdType { get; set; }
        public string RawProductCode { get; set; } = string.Empty;
    }
}
