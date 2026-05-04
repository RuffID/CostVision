using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Связь пользователя со счётом и его роль в совместном доступе.
    /// </summary>
    public class AccountMember
    {
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public Guid AccountId { get; set; }

        public Account Account { get; set; } = null!;

        public AccountAccessRole Role { get; set; }
    }
}
