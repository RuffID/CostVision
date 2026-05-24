namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class DeleteMoneyMovementRequest
    {
        public Guid MoneyMovementId { get; set; }

        public Guid AccountId { get; set; }
    }
}
