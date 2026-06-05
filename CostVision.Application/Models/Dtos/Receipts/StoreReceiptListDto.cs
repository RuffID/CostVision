namespace CostVision.Application.Models.Dtos.Receipts
{
    public class StoreReceiptListDto
    {
        public List<ReceiptDto> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }

        public bool HasPreviousPage { get; set; }

        public bool HasNextPage { get; set; }
    }
}
