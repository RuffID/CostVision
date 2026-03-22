namespace CostVision.Models.Requests.Receipts
{
    public class UserAccountViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string ColorHex { get; set; } = CostVision.Models.Receipts.Account.DEFAULT_COLOR_HEX;

        public bool IsActive { get; set; }

        public bool CanManage { get; set; }
    }
}
