namespace CostVision.Application.Models.Requests.Receipts
{
    /// <summary>
    /// Запрос на назначение счёта чеку или перенос связи чека из одного счёта в другой.
    /// </summary>
    public class MoveReceiptToAccountRequest
    {
        public Guid ReceiptId { get; set; }

        public Guid SourceAccountId { get; set; }

        public Guid TargetAccountId { get; set; }
    }
}
