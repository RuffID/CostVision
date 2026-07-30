using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Связь пользователя со счётом и его роль в совместном доступе.
    /// </summary>
    public class AccountMember
    {
        internal AccountMember()
        {
        }

        public Guid UserId { get; internal set; }

        public User User { get; internal set; } = null!;

        public Guid AccountId { get; internal set; }

        public Account Account { get; internal set; } = null!;

        public AccountAccessRole Role { get; internal set; }

        internal static AccountMember CreateOwner(Account account, Guid userId)
        {
            return new AccountMember
            {
                Account = account,
                AccountId = account.Id,
                UserId = userId,
                Role = AccountAccessRole.Owner
            };
        }

        internal static AccountMember Create(Account account, Guid userId, AccountAccessRole role)
        {
            return new AccountMember
            {
                Account = account,
                AccountId = account.Id,
                UserId = userId,
                Role = role
            };
        }

        internal void ChangeRole(AccountAccessRole role)
        {
            Role = role;
        }
    }
}
