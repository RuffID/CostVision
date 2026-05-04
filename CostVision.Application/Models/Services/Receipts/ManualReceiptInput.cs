using CostVision.Domain.Models.Enums.Receipts;

namespace CostVision.Application.Models.Services.Receipts
{
    public class ManualReceiptInput
    {
        public string FiscalDriveNumber { get; set; } = string.Empty;     // ФН

        public string FiscalDocumentNumber { get; set; } = string.Empty;  // ФД

        public string FiscalSign { get; set; } = string.Empty;            // ФПД

        public decimal Sum { get; set; }

        public DateTime DateTime { get; set; }

        public ReceiptOperationType OperationType { get; set; }
    }
}
