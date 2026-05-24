using CostVision.Domain.Models.Enums.MoneyMovements;

namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class CreateMoneyMovementRequest
    {
        public Guid AccountId { get; set; }

        public decimal Amount { get; set; }

        public MoneyMovementType? Type { get; set; }

        public DateTime OccurredAt { get; set; }

        public string? Comment { get; set; }

        public Guid? PerformedByUserId { get; set; }
    }
}
