using CostVision.Models.Authorization;
using CostVision.Models.Enums.Authorization;

namespace CostVision.Models.Receipts
{
    /// <summary>
    /// Связь User-Account (совместные счета)
    /// </summary>
    public class AccountMember
    {
        // Пользователь
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;

        // Счет
        public Guid AccountId { get; set; }
        public virtual Account Account { get; set; } = null!;

        // Роль доступа к счету
        public AccountAccessRole Role { get; set; }
    }
}