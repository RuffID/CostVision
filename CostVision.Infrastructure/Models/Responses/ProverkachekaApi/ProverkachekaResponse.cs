namespace CostVision.Infrastructure.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaResponse
    {
        public int Code { get; set; }
        public int First { get; set; }
        public ProverkachekaData? Data { get; set; }
        public ProverkachekaRequestInfo? Request { get; set; }
    }
}
