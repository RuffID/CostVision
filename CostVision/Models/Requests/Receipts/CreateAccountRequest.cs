namespace CostVision.Models.Requests.Receipts
{
    public class CreateAccountRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
    }
}
