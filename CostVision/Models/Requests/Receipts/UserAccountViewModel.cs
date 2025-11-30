namespace CostVision.Models.Requests.Receipts
{
    public class UserAccountViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
    }
}
