using CostVision.Models.Enums.Authorization;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Services.Receipts;

namespace CostVision.Interfaces.Service.Receipts
{
    public interface IAccountService
    {
        Task<Account> CreateAccountAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct);

        Task<List<UserAccountViewModel>> GetUserAccountsAsync(Guid userId, bool includeArchived, CancellationToken ct);

        Task<bool> UpdateAccountAsync(Guid ownerUserId, Account account, CancellationToken ct);

        Task<AccountMember> AddMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, AccountAccessRole role, CancellationToken ct);

        Task RemoveMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, CancellationToken ct);

        Task<ReceiptAccountLinkResult> LinkReceiptToAccountAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct);
    }
}
