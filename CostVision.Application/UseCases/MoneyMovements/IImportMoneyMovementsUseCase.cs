using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IImportMoneyMovementsUseCase
    {
        Task<ServiceResult<bool>> ExecuteAsync(SaveBankStatementImportRequest request, Guid currentUserId, CancellationToken ct);
    }
}
