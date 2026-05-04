namespace CostVision.Application.Models.Requests.Receipts
{
    public class UpdateAccountRequest
    {
        public Guid AccountId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string ColorHex { get; set; } = CostVision.Domain.Models.Receipts.Account.DEFAULT_COLOR_HEX;

        public bool IsActive { get; set; }
    }
}
