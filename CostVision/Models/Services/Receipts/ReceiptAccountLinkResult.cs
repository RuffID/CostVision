using CostVision.Models.Enums.Services.Receipts;
using CostVision.Models.Receipts;

namespace CostVision.Models.Services.Receipts
{
    public class ReceiptAccountLinkResult
    {
        public ReceiptAccountLinkStatusEnum Status { get; set; }

        public string? ErrorMessage { get; set; }

        public ReceiptAccount? Link { get; set; }
    }
}
