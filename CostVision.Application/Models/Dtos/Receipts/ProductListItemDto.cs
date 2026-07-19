namespace CostVision.Application.Models.Dtos.Receipts
{
    public class ProductListItemDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? AdaptiveName { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public int ReceiptCount { get; set; }

        public decimal? AveragePrice { get; set; }

        public bool? AveragePriceIsWeighted { get; set; }
    }
}
