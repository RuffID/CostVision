using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Domain.Models.Receipts
{
    /// <summary>
    /// Связь пользователя со счётом и его роль в совместном доступе.
    /// </summary>
    public class AccountMember
    {
        private AccountMember()
        {
        }

        public Guid UserId { get; private set; }

        public User User { get; private set; } = null!;

        public Guid AccountId { get; private set; }

        public Account Account { get; private set; } = null!;

        public AccountAccessRole Role { get; private set; }

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
