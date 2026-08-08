using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface ILinkMoneyMovementReceiptUseCase
    {
        Task<ServiceResult> ExecuteAsync(LinkMoneyMovementReceiptRequest request, Guid currentUserId, CancellationToken ct);
    }
}
