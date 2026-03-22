namespace CostVision.Models.Requests.Receipts
{
    /// <summary>
    /// Запрос на удаление связи чека со счётом.
    /// </summary>
    public class RemoveReceiptFromAccountRequest
    {
        public Guid ReceiptId { get; set; }

        public Guid AccountId { get; set; }
    }
}
