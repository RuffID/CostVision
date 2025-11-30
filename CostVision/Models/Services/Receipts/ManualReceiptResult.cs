using CostVision.Models.Receipts;

namespace CostVision.Models.Services.Receipts
{
    public class ManualReceiptResult
    {
        public bool IsCreated { get; set; }

        public string? ErrorMessage { get; set; }

        public Receipt? Receipt { get; set; }
    }
}
