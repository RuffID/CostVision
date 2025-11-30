namespace CostVision.Models.Services.Receipts
{
    public class QrParsed
    {
        public DateTime DateTime { get; set; }
        public decimal? Sum { get; set; }
        public string FiscalDriveNumber { get; set; } = string.Empty;
        public string FiscalDocumentNumber { get; set; } = string.Empty;
        public string FiscalSign { get; set; } = string.Empty;
        public int? OperationType { get; set; }
    }
}