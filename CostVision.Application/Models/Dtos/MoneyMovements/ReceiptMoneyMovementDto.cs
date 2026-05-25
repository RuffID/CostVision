using CostVision.Domain.Models.Enums.MoneyMovements;

namespace CostVision.Application.Models.Dtos.MoneyMovements
{
    public class ReceiptMoneyMovementDto
    {
        public Guid MoneyMovementId { get; set; }

        public DateTime OccurredAt { get; set; }

        public decimal Amount { get; set; }

        public MoneyMovementType Type { get; set; }

        public string Comment { get; set; } = string.Empty;

        public string ImportComment { get; set; } = string.Empty;

        public string AccountName { get; set; } = string.Empty;

        public bool IsLinkedToOtherReceipt { get; set; }
    }
}
