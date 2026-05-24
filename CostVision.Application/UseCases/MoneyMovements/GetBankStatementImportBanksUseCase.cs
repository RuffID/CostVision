using CostVision.Application.Models.Dtos.MoneyMovements;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetBankStatementImportBanksUseCase : IGetBankStatementImportBanksUseCase
    {
        public Task<List<BankStatementImportBankDto>> ExecuteAsync(CancellationToken ct)
        {
            List<BankStatementImportBankDto> banks =
            [
                new BankStatementImportBankDto
                {
                    Id = BankStatementImportConstants.T_BANK_PDF_BANK_ID,
                    Name = "Т-Банк",
                    Description = "PDF-выписка"
                }
            ];

            return Task.FromResult(banks);
        }
    }
}
