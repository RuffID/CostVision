using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Domain.Models.MoneyMovements
{
    /// <summary>
    /// Связь операции движения денег с чеком.
    /// </summary>
    public class MoneyMovementReceipt
    {
        internal MoneyMovementReceipt()
        {
        }

        public Guid MoneyMovementId { get; internal set; }

        public MoneyMovement? MoneyMovement { get; internal set; }

        public Guid ReceiptId { get; internal set; }

        public Receipt? Receipt { get; internal set; }

        public Guid CreatedByUserId { get; internal set; }

        public User? CreatedByUser { get; internal set; }

        public DateTime CreatedAtUtc { get; internal set; }

        /// <summary>
        /// Создаёт допустимую связь операции движения денег с чеком.
        /// </summary>
        public static bool TryCreate(
            Guid moneyMovementId,
            Guid receiptId,
            Guid createdByUserId,
            DateTime createdAtUtc,
            out MoneyMovementReceipt? link,
            out string? error)
        {
            link = null;

            if (moneyMovementId == Guid.Empty || receiptId == Guid.Empty)
            {
                error = "Некорректный идентификатор операции или чека.";
                return false;
            }

            if (createdByUserId == Guid.Empty)
            {
                error = "Некорректный идентификатор пользователя, создавшего связь.";
                return false;
            }

            if (createdAtUtc == default)
            {
                error = "Дата создания связи не заполнена.";
                return false;
            }

            link = new MoneyMovementReceipt
            {
                MoneyMovementId = moneyMovementId,
                ReceiptId = receiptId,
                CreatedByUserId = createdByUserId,
                CreatedAtUtc = createdAtUtc
            };
            error = null;
            return true;
        }
    }
}
