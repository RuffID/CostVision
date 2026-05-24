using CostVision.Domain.Models.Enums.MoneyMovements;

namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class BankStatementImportRowRequest
    {
        public DateTime OccurredAt { get; set; }

        public decimal Amount { get; set; }

        public MoneyMovementType Type { get; set; }

        public string? Comment { get; set; }

        public string ImportComment { get; set; } = string.Empty;

        public Guid? DuplicateMoneyMovementId { get; set; }

        public bool ReplaceDuplicate { get; set; }
    }
}
