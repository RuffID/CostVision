using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IUpdateMoneyMovementCommentUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(UpdateMoneyMovementCommentRequest request, Guid currentUserId, CancellationToken ct);
    }
}
