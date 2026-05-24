namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class LinkMoneyMovementReceiptRequest
    {
        public Guid MoneyMovementId { get; set; }

        public Guid ReceiptId { get; set; }
    }
}
