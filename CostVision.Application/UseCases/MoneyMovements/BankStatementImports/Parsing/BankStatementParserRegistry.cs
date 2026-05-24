namespace CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing
{
    public class BankStatementParserRegistry(IEnumerable<IBankStatementParser> parsers) : IBankStatementParserRegistry
    {
        private readonly List<IBankStatementParser> parsers = parsers.ToList();

        public IReadOnlyList<IBankStatementParser> GetParsers()
        {
            return parsers;
        }

        public IBankStatementParser? FindByBankId(string bankId)
        {
            return parsers.FirstOrDefault(parser => string.Equals(parser.BankId, bankId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
