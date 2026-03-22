namespace CostVision.Models.Dtos.Receipts
{
    public class AccountShareUserDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Login { get; set; } = string.Empty;

        public bool IsSelected { get; set; }
    }
}
