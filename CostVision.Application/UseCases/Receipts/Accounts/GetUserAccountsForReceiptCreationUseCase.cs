using CostVision.Application.Models.Requests.Receipts;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class GetUserAccountsForReceiptCreationUseCase(IGetUserAccountsUseCase getUserAccountsUseCase) : IGetUserAccountsForReceiptCreationUseCase
    {
        public async Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, CancellationToken ct)
        {
            List<UserAccountViewModel> accounts = await getUserAccountsUseCase.ExecuteAsync(userId, includeArchived: false, ct);

            return accounts
                .Where(account => account.AccessRole == AccountAccessRole.Owner || account.AccessRole == AccountAccessRole.Editor)
                .ToList();
        }
    }
}
