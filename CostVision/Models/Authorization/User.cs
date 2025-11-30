using CostVision.Interfaces.Entity;
using CostVision.Models.Receipts;

namespace CostVision.Models.Authorization
{
    public class User : IEntity<Guid>, ICopyable<User>
    {
        public Guid Id { get; set; }

        public string Login { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? LastLoginAtUtc { get; set; }

        public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        // Связи с счетами (совместные/личные)
        public virtual List<AccountMember> AccountMemberships { get; set; } = new List<AccountMember>();

        // Чеки, загруженные пользователем (как автор импорта)
        public virtual List<Receipt> CreatedReceipts { get; set; } = new List<Receipt>();

        public virtual List<ExpenseCategory> Categories { get; set; } = new List<ExpenseCategory>();

        public virtual List<Account> Accounts { get; set; } = new List<Account>();

        public void CopyData(User newItem)
        {
            Login = newItem.Login;
            PasswordHash = newItem.PasswordHash;
            Name = newItem.Name;
            IsActive = newItem.IsActive;
            CreatedAtUtc = newItem.CreatedAtUtc;
            LastLoginAtUtc = newItem.LastLoginAtUtc;
        }
    }
}
