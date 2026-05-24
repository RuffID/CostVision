using CostVision.Application.Models.Dtos.MoneyMovements;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public interface IGetBankStatementImportBanksUseCase
    {
        Task<List<BankStatementImportBankDto>> ExecuteAsync(CancellationToken ct);
    }
}
