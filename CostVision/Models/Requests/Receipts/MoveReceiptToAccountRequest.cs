namespace CostVision.Models.Requests.Receipts
{
    /// <summary>
    /// Запрос на перенос связи чека из одного счёта в другой.
    /// </summary>
    public class MoveReceiptToAccountRequest
    {
        public Guid ReceiptId { get; set; }

        public Guid SourceAccountId { get; set; }

        public Guid TargetAccountId { get; set; }
    }
}
