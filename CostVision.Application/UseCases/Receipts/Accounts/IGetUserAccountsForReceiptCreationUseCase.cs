using CostVision.Application.Models.Requests.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IGetUserAccountsForReceiptCreationUseCase
    {
        Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, CancellationToken ct);
    }
}
