namespace CostVision.Application.Models.Dtos.Receipts
{
    public class StoreListItemDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string? AdaptiveName { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public int ReceiptCount { get; set; }

        public decimal TotalSpent { get; set; }

        public string GroupKey { get; set; } = string.Empty;

        public List<StoreListItemDto> Children { get; set; } = new();
    }
}
