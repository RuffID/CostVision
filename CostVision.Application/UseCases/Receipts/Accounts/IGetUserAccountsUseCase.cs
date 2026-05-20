using CostVision.Application.Models.Requests.Receipts;

namespace CostVision.Application.UseCases.Receipts.Accounts
{
    public interface IGetUserAccountsUseCase
    {
        Task<List<UserAccountViewModel>> ExecuteAsync(Guid userId, bool includeArchived, CancellationToken ct);
    }
}
