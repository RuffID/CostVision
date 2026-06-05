namespace CostVision.Application.Models.Requests.Receipts
{
    public class GetStoreReceiptListRequest
    {
        public Guid? StoreId { get; set; }

        public string? GroupKey { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
