namespace CostVision.Models.Receipts
{
    /// <summary>
    /// Связь Receipt-Account (чек в нескольких счетах)
    /// Это many-to-many между Receipt и Account.
    /// Один чек → много счетов. 
    /// Один счет → много чеков.
    /// </summary>
    public class ReceiptAccount
    {
        public Guid ReceiptId { get; set; }
        public virtual Receipt? Receipt { get; set; }

        public Guid AccountId { get; set; }
        public virtual Account? Account { get; set; }
    }
}
