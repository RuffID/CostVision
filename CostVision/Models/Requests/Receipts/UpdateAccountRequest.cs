namespace CostVision.Models.Requests.Receipts
{
    public class UpdateAccountRequest
    {
        public Guid AccountId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
    }
}
