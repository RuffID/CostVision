using CostVision.Models.Receipts;

namespace CostVision.Models.Requests.Receipts
{
    public class CreateAccountRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ColorHex { get; set; } = Account.DEFAULT_COLOR_HEX;
    }
}
