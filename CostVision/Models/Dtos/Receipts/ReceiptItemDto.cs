namespace CostVision.Models.Dtos.Receipts
{
    public class ReceiptItemDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Sum { get; set; }
    }
}
