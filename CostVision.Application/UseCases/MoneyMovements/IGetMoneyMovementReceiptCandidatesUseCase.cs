using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IGetMoneyMovementReceiptCandidatesUseCase
    {
        Task<ServiceResult<List<MoneyMovementReceiptDto>>> ExecuteAsync(GetMoneyMovementReceiptCandidatesRequest request, Guid currentUserId, CancellationToken ct);
    }
}
