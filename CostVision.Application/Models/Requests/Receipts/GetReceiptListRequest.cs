namespace CostVision.Application.Models.Requests.Receipts
{
    public class GetReceiptListRequest
    {
        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }
    }
}
