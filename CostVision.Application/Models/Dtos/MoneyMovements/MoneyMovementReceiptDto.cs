namespace CostVision.Application.Models.Dtos.MoneyMovements
{
    public class MoneyMovementReceiptDto
    {
        public Guid ReceiptId { get; set; }

        public DateTime DateTime { get; set; }

        public string RetailPlace { get; set; } = string.Empty;

        public decimal TotalSum { get; set; }

        public string AccountName { get; set; } = string.Empty;

        public bool IsLinkedToOtherMoneyMovement { get; set; }
    }
}
