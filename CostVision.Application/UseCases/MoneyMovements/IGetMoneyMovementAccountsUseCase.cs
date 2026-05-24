using CostVision.Application.Models.Requests.Receipts;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IGetMoneyMovementAccountsUseCase
    {
        Task<List<UserAccountViewModel>> ExecuteAsync(Guid currentUserId, CancellationToken ct);
    }
}
