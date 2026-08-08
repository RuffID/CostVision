using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public class GetUserAccountsForReceiptCreationUseCase(IGetUserAccountsUseCase getUserAccountsUseCase) : IGetUserAccountsForReceiptCreationUseCase
    {
        public async Task<ServiceResult<List<UserAccountViewModel>>> ExecuteAsync(Guid userId, CancellationToken ct)
        {
            ServiceResult<List<UserAccountViewModel>> accountsResult = await getUserAccountsUseCase.ExecuteAsync(userId, includeArchived: false, ct);
            if (!accountsResult.Success)
                return accountsResult.PropagateFailure<List<UserAccountViewModel>>();

            List<UserAccountViewModel> accounts = accountsResult.Data
                .Where(account => account.AccessRole == AccountAccessRole.Owner || account.AccessRole == AccountAccessRole.Editor)
                .ToList();

            return ServiceResult<List<UserAccountViewModel>>.Ok(accounts);
        }
    }
}
