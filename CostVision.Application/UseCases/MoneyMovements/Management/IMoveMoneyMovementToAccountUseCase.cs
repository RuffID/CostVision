using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IMoveMoneyMovementToAccountUseCase
    {
        Task<ServiceResult> ExecuteAsync(MoveMoneyMovementToAccountRequest request, Guid currentUserId, CancellationToken ct);
    }
}
