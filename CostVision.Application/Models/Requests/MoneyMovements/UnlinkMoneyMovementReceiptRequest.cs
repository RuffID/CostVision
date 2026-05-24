namespace CostVision.Application.Models.Requests.MoneyMovements
{
    public class UnlinkMoneyMovementReceiptRequest
    {
        public Guid MoneyMovementId { get; set; }

        public Guid ReceiptId { get; set; }
    }
}
