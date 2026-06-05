namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class UpdateMoneyMovementCommentRequest
    {
        public Guid MoneyMovementId { get; set; }

        public Guid? AccountId { get; set; }

        public string? Comment { get; set; }
    }
}
