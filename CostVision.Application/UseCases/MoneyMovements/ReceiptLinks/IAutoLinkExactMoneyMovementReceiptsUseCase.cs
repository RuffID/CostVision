using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IAutoLinkExactMoneyMovementReceiptsUseCase
    {
        Task<ServiceResult<int>> ExecuteAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct);
    }
}
