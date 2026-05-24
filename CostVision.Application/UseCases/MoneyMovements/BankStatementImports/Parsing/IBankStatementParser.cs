using CostVision.Application.Models.Dtos.MoneyMovements;

namespace CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing
{
    public interface IBankStatementParser
    {
        string BankId { get; }

        string BankName { get; }

        string Description { get; }

        bool IsConfigured { get; }

        bool CanParseFile(string fileName);

        BankStatementImportPreviewDto Parse(Guid accountId, IReadOnlyList<string> pages);
    }
}
