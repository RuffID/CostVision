using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.Models.Services.Receipts
{
    public class ManualReceiptResult
    {
        public bool IsCreated { get; set; }

        public string? ErrorMessage { get; set; }

        public Receipt? Receipt { get; set; }
    }
}
