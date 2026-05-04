using CostVision.Application.Models.Enums.Services.Receipts;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Application.Models.Services.Receipts
{
    public class ReceiptAccountLinkResult
    {
        public ReceiptAccountLinkStatusEnum Status { get; set; }

        public string? ErrorMessage { get; set; }

        public ReceiptAccount? Link { get; set; }
    }
}
