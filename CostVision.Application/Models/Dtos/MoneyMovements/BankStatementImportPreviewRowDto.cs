using CostVision.Domain.Models.Enums.MoneyMovements;

namespace CostVision.Application.Models.Dtos.MoneyMovements
{
    public class BankStatementImportPreviewRowDto
    {
        public string ClientRowId { get; set; } = string.Empty;

        public DateTime OccurredAt { get; set; }

        public DateTime ProcessedAt { get; set; }

        public decimal Amount { get; set; }

        public MoneyMovementType Type { get; set; }

        public string Comment { get; set; } = string.Empty;

        public string ImportComment { get; set; } = string.Empty;

        public bool IsDuplicate { get; set; }

        public Guid? DuplicateMoneyMovementId { get; set; }

        public int SourceLineNumber { get; set; }
    }
}
