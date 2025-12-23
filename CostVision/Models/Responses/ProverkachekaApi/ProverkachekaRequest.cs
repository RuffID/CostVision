using CostVision.Models.Enums.Receipts;

namespace CostVision.Models.Responses.ProverkachekaApi
{
    public class ProverkachekaRequest
    {
        public string? Fn { get; set; }

        public string? Fd { get; set; }

        public string? Fp { get; set; }

        public string? CheckTime { get; set; }

        public ReceiptOperationType? Type { get; set; }

        public string? Sum { get; set; }
    }
}
