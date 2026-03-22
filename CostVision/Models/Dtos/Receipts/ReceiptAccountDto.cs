namespace CostVision.Models.Dtos.Receipts
{
    public class ReceiptAccountDto
    {
        public Guid Id { get; set; }

        public Guid ReceiptId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string ColorHex { get; set; } = CostVision.Models.Receipts.Account.DEFAULT_COLOR_HEX;

        public bool CanEditReceipt { get; set; }
    }
}
