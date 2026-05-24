using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;

namespace CostVision.Application.UseCases.MoneyMovements
{
    public class GetBankStatementImportBanksUseCase(IBankStatementParserRegistry parserRegistry) : IGetBankStatementImportBanksUseCase
    {
        public Task<List<BankStatementImportBankDto>> ExecuteAsync(CancellationToken ct)
        {
            List<BankStatementImportBankDto> banks = parserRegistry.GetParsers()
                .Select(parser => new BankStatementImportBankDto
                {
                    Id = parser.BankId,
                    Name = parser.BankName,
                    Description = parser.Description
                })
                .ToList();

            return Task.FromResult(banks);
        }
    }
}
