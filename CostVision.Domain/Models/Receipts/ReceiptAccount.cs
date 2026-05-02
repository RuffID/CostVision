namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Связь чека со счётом, позволяющая относить один чек к нескольким счетам.
    /// </summary>
    public class ReceiptAccount
    {
        public Guid ReceiptId { get; set; }

        public Receipt? Receipt { get; set; }

        public Guid AccountId { get; set; }

        public Account? Account { get; set; }
    }
}
