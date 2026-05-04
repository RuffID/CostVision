namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaRequestInfo
    {
        public string? QrUrl { get; set; }
        public string? QrFile { get; set; }
        public string? QrRaw { get; set; }
        public ProverkachekaRequest? Manual { get; set; }
    }
}
