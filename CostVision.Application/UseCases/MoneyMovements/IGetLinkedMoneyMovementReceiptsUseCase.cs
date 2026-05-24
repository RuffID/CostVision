using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IGetLinkedMoneyMovementReceiptsUseCase
    {
        Task<ServiceResult<List<MoneyMovementReceiptDto>>> ExecuteAsync(Guid moneyMovementId, Guid currentUserId, CancellationToken ct);
    }
}
