using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IGetMoneyMovementListUseCase
    {
        Task<ServiceResult<MoneyMovementListDto>> ExecuteAsync(Guid currentUserId, DateTime dateFrom, DateTime dateTo, Guid? accountId, CancellationToken ct, int page = 1, int pageSize = 20, string? search = null, string? receiptFilter = null);
    }
}
