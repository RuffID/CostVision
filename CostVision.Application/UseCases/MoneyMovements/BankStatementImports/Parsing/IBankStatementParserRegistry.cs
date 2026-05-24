namespace CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing
{
    public interface IBankStatementParserRegistry
    {
        IReadOnlyList<IBankStatementParser> GetParsers();

        IBankStatementParser? FindByBankId(string bankId);
    }
}
