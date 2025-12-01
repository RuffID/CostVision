using CostVision.Models.Services.Receipts;

namespace CostVision.Models.Requests.Receipts
{
    public class ReceiptManualCreateRequest
    {
        public ManualReceiptInput Receipt { get; set; } = new();
        public Guid AccountId { get; set; }
    }        
}