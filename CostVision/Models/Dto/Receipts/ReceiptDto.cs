namespace CostVision.Models.Dto.Receipts
{
    public class ReceiptDto
    {
        public Guid Id { get; set; }
        public DateTime DateTime { get; set; }
        public string RetailPlace { get; set; } = string.Empty;
        public string RetailPlaceAddress { get; set; } = string.Empty;
        public decimal TotalSum { get; set; }
        public string FiscalDriveNumber { get; set; } = string.Empty;
        public string FiscalDocumentNumber { get; set; } = string.Empty;
        public string FiscalSign { get; set; } = string.Empty;
        public ICollection<ReceiptItemDto> Items { get; set; } = new List<ReceiptItemDto>();
    }
}
