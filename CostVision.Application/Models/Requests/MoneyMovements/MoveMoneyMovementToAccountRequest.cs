namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class MoveMoneyMovementToAccountRequest
    {
        public Guid MoneyMovementId { get; set; }

        public Guid SourceAccountId { get; set; }

        public Guid TargetAccountId { get; set; }
    }
}
