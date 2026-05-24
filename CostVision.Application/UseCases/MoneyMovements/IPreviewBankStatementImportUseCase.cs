using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Responses.Results;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IPreviewBankStatementImportUseCase
    {
        Task<ServiceResult<BankStatementImportPreviewDto>> ExecuteAsync(string bankId, Guid accountId, string fileName, Stream fileStream, Guid currentUserId, CancellationToken ct);
    }
}
