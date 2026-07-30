using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;

namespace CostVision.Domain.Models.MoneyMovements
{
    /// <summary>
    /// Связь операции движения денег с чеком.
    /// </summary>
    public class MoneyMovementReceipt
    {
        private MoneyMovementReceipt()
        {
        }

        public Guid MoneyMovementId { get; private set; }

        public MoneyMovement? MoneyMovement { get; private set; }

        public Guid ReceiptId { get; private set; }

        public Receipt? Receipt { get; private set; }

        public Guid CreatedByUserId { get; private set; }

        public User? CreatedByUser { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

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

        /// <summary>
        /// Создаёт связь и согласованно добавляет её в навигацию чека.
        /// </summary>
        public static bool TryCreate(
            Guid moneyMovementId,
            Receipt receipt,
            Guid createdByUserId,
            DateTime createdAtUtc,
            out MoneyMovementReceipt? link,
            out string? error)
        {
            link = null;

            if (receipt == null)
            {
                error = "Чек не указан.";
                return false;
            }

            if (!TryCreate(
                    moneyMovementId,
                    receipt.Id,
                    createdByUserId,
                    createdAtUtc,
                    out link,
                    out error))
                return false;

            link!.Receipt = receipt;
            if (!receipt.TryAttachMoneyMovementLink(link, out error))
            {
                link = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Создаёт связь и согласованно задаёт обе навигации.
        /// </summary>
        public static bool TryCreate(
            MoneyMovement moneyMovement,
            Receipt receipt,
            Guid createdByUserId,
            DateTime createdAtUtc,
            out MoneyMovementReceipt? link,
            out string? error)
        {
            link = null;

            if (moneyMovement == null || receipt == null)
            {
                error = "Операция движения денег или чек не указан.";
                return false;
            }

            if (moneyMovement.ReceiptLinks.Any(item => item.ReceiptId == receipt.Id))
            {
                error = "Чек уже привязан к операции.";
                return false;
            }

            if (!TryCreate(
                    moneyMovement.Id,
                    receipt,
                    createdByUserId,
                    createdAtUtc,
                    out link,
                    out error))
                return false;

            link!.MoneyMovement = moneyMovement;
            if (!moneyMovement.TryAttachReceiptLink(link, out error))
            {
                link = null;
                return false;
            }

            return true;
        }
    }
}
