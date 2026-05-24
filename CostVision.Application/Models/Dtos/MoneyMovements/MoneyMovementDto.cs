using CostVision.Domain.Models.Enums.MoneyMovements;

namespace CostVision.Application.Models.Dtos.MoneyMovements
{
    public class MoneyMovementDto
    {
        public Guid Id { get; set; }

        public Guid AccountId { get; set; }

        public string AccountName { get; set; } = string.Empty;

        public string AccountColorHex { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public MoneyMovementType Type { get; set; }

        public DateTime OccurredAt { get; set; }

        public string? Comment { get; set; }

        public string? ImportComment { get; set; }

        public Guid PerformedByUserId { get; set; }

        public string PerformedByUserName { get; set; } = string.Empty;

        public MoneyMovementSource Source { get; set; }

        public int LinkedReceiptCount { get; set; }

        public int AvailableReceiptCount { get; set; }

        public decimal LinkedReceiptsTotalSum { get; set; }
    }
}
