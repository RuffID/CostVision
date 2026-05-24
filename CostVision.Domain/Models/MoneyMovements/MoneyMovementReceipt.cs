using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Domain.Models.MoneyMovements
{
    /// <summary>
    /// Связь операции движения денег с чеком.
    /// </summary>
    public class MoneyMovementReceipt
    {
        public Guid MoneyMovementId { get; set; }

        public MoneyMovement? MoneyMovement { get; set; }

        public Guid ReceiptId { get; set; }

        public Receipt? Receipt { get; set; }

        public Guid CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
