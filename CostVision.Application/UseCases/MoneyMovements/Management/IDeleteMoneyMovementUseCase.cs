using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IDeleteMoneyMovementUseCase
    {
        Task<ServiceResult> ExecuteAsync(DeleteMoneyMovementRequest request, Guid currentUserId, CancellationToken ct);
    }
}
