namespace CostVision.Application.Models.Dtos.Receipts
{
    public class ProductListDto
    {
        public List<ProductListItemDto> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages { get; set; }

        public bool HasPreviousPage { get; set; }

        public bool HasNextPage { get; set; }

        public decimal TotalSum { get; set; }
    }
}
