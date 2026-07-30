namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Связь чека со счётом, позволяющая относить один чек к нескольким счетам.
    /// </summary>
    public class ReceiptAccount
    {
        private ReceiptAccount()
        {
        }

        public Guid ReceiptId { get; private set; }

        public Receipt Receipt { get; private set; } = null!;

        public Guid AccountId { get; private set; }

        public Account? Account { get; private set; }

        internal static ReceiptAccount Create(Receipt receipt, Guid accountId)
        {
            return new ReceiptAccount
            {
                ReceiptId = receipt.Id,
                Receipt = receipt,
                AccountId = accountId
            };
        }

        internal void AttachAccount(Account account)
        {
            if (account.Id != AccountId)
                throw new InvalidOperationException("Идентификатор счёта не соответствует связи.");

            Account = account;
        }
    }
}
