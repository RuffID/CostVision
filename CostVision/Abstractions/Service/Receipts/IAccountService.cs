using CostVision.Models.Enums.Authorization;
using CostVision.Models.Dtos.Receipts;
using CostVision.Models.Receipts;
using CostVision.Models.Requests.Receipts;
using CostVision.Models.Responses.Results;
using CostVision.Models.Services.Receipts;

namespace CostVision.Abstractions.Service.Receipts
{
    public interface IAccountService
    {
        Task<ServiceResult<Account>> CreateAccountAsync(Guid ownerUserId, CreateAccountRequest request, CancellationToken ct);

        Task<List<UserAccountViewModel>> GetUserAccountsAsync(Guid userId, bool includeArchived, CancellationToken ct);

        Task<ServiceResult<Account>> UpdateAccountAsync(Guid ownerUserId, Account account, CancellationToken ct);

        Task<ServiceResult<List<AccountShareUserDto>>> GetAccountShareUsersAsync(Guid accountId, Guid ownerUserId, CancellationToken ct);

        Task<ServiceResult<bool>> UpdateAccountMembersAsync(Guid accountId, Guid ownerUserId, IReadOnlyCollection<Guid> userIds, CancellationToken ct);

        Task<AccountMember> AddMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, AccountAccessRole role, CancellationToken ct);

        Task RemoveMemberAsync(Guid accountId, Guid ownerUserId, Guid targetUserId, CancellationToken ct);

        Task<ReceiptAccountLinkResult> LinkReceiptToAccountAsync(Guid accountId, Guid receiptId, Guid currentUserId, CancellationToken ct);
    }
}
