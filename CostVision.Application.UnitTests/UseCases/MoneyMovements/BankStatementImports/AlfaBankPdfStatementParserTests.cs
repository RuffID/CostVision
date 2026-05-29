using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements.BankStatementImports;

public class AlfaBankPdfStatementParserTests
{
    [Fact]
    public void Parser_ExposesCurrentStubConfiguration()
    {
        IBankStatementParser parser = CreateParser();
        Guid accountId = Guid.NewGuid();

        BankStatementImportPreviewDto preview = parser.Parse(accountId, ["01.02.2026 operation 100.00"]);

        Assert.Equal("alfa-bank-pdf", parser.BankId);
        Assert.False(parser.IsConfigured);
        Assert.True(parser.CanParseFile("statement.PDF"));
        Assert.False(parser.CanParseFile("statement.xlsx"));
        Assert.Equal(accountId, preview.AccountId);
        Assert.Equal(parser.BankId, preview.BankId);
        Assert.Empty(preview.Rows);
        Assert.Empty(preview.Errors);
    }

    private static IBankStatementParser CreateParser()
    {
        Type parserType = typeof(IBankStatementParser).Assembly.GetType(
            "CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsers.Alfa.AlfaBankPdfStatementParser",
            true)!;

        return (IBankStatementParser)Activator.CreateInstance(parserType, nonPublic: true)!;
    }
}
