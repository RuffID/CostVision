namespace CostVision.Application.Models.Dtos.Receipts
{
    public class ProductStorePurchaseDto
    {
        public string StoreName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal PricePerUnit { get; set; }

        public bool IsWeighted { get; set; }
    }
}
