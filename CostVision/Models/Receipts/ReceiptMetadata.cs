namespace CostVision.Models.Receipts
{
    /// <summary>
    /// Дополнительная информация по организации в чеке
    /// </summary>
    public class ReceiptMetadata
    {
        public string? Id { get; set; }
        public string? OfdId { get; set; }
        public string? Address { get; set; }
        public string? Subtype { get; set; }
        public string? ReceiveDate { get; set; }
    }
}
