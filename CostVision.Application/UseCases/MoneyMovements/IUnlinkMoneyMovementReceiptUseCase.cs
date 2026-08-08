using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IUnlinkMoneyMovementReceiptUseCase
    {
        Task<ServiceResult> ExecuteAsync(UnlinkMoneyMovementReceiptRequest request, Guid currentUserId, CancellationToken ct);
    }
}
