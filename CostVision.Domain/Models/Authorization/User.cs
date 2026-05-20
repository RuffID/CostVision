using CostVision.Domain.Models.Receipts;
using EFCoreLibrary.Abstractions.Entity;

namespace CostVision.Domain.Models.Authorization
{
    public class User : IEntity<Guid>
    {
        public Guid Id { get; set; }

        public string Login { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? LastLoginAtUtc { get; set; }

        public ICollection<Role> Roles { get; set; } = new List<Role>();

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

        public List<AccountMember> AccountMemberships { get; set; } = new List<AccountMember>();

        public List<Receipt> CreatedReceipts { get; set; } = new List<Receipt>();

        public List<ExpenseCategory> Categories { get; set; } = new List<ExpenseCategory>();

        public List<Account> Accounts { get; set; } = new List<Account>();

        /// <summary>
        /// Активирует пользователя.
        /// </summary>
        public void Activate()
        {
            IsActive = true;
        }

        /// <summary>
        /// Деактивирует пользователя.
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// Обновляет время последнего успешного входа.
        /// </summary>
        public void MarkLogin(DateTime loginAtUtc)
        {
            MarkActivity(loginAtUtc);
        }

        /// <summary>
        /// Обновляет время последней активности пользователя.
        /// </summary>
        public void MarkActivity(DateTime activityAtUtc)
        {
            LastLoginAtUtc = activityAtUtc;
        }
    }
}
