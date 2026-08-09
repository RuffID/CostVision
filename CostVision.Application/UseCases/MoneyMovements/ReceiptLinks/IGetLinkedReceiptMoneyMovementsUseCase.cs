using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IGetLinkedReceiptMoneyMovementsUseCase
    {
        Task<ServiceResult<List<ReceiptMoneyMovementDto>>> ExecuteAsync(Guid receiptId, Guid currentUserId, CancellationToken ct);
    }
}
