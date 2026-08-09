using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IImportMoneyMovementsUseCase
    {
        Task<ServiceResult<BankStatementImportResultDto>> ExecuteAsync(SaveBankStatementImportRequest request, Guid currentUserId, CancellationToken ct);
    }
}
