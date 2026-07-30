namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Связь чека со счётом, позволяющая относить один чек к нескольким счетам.
    /// </summary>
    public class ReceiptAccount
    {
        internal ReceiptAccount()
        {
        }

        public Guid ReceiptId { get; internal set; }

        public Receipt Receipt { get; internal set; } = null!;

        public Guid AccountId { get; internal set; }

        public Account? Account { get; set; }

        internal static ReceiptAccount Create(Receipt receipt, Guid accountId)
        {
            return new ReceiptAccount
            {
                ReceiptId = receipt.Id,
                Receipt = receipt,
                AccountId = accountId
            };
        }
    }
}
