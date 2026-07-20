namespace CostVision.Application.Models.Requests.Receipts
{
    public class GetReceiptListRequest
    {
        public DateTime DateFrom { get; set; }

        public DateTime DateTo { get; set; }

        public Guid? AccountId { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;

        public string? Search { get; set; }

        public string? SearchMode { get; set; }

        public string? OperationFilter { get; set; }
    }
}
