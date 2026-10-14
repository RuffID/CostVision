using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IGetReceiptMoneyMovementCandidatesUseCase
    {
        Task<ServiceResult<PagedListDto<ReceiptMoneyMovementDto>>> ExecuteAsync(GetReceiptMoneyMovementCandidatesRequest request, Guid currentUserId, CancellationToken ct);
    }
}
