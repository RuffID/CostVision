using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using CostVision.Domain.Models.Enums.MoneyMovements;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements.BankStatementImports;

public class TBankPdfStatementParserTests
{
    private const string CARD_BLOCK_HEADER = "Операции по карте № 1234";
    private const string RUBLE = "₽";

    [Fact]
    public void CanParseFile_ReturnsTrueOnlyForPdfFiles()
    {
        IBankStatementParser parser = CreateParser();

        Assert.True(parser.CanParseFile("statement.PDF"));
        Assert.False(parser.CanParseFile("statement.txt"));
    }

    [Fact]
    public void Parse_ReadsValidCombinedRowsWithIncomeAndExpenseAmounts()
    {
        Guid accountId = Guid.NewGuid();
        IBankStatementParser parser = CreateParser();

        BankStatementImportPreviewDto preview = parser.Parse(accountId,
        [
            string.Join('\n',
            [
                CARD_BLOCK_HEADER,
                $"01.02.26 13:45 02.02.26 -1 234,56 {RUBLE} -1 234,56 {RUBLE} Grocery",
                $"03.02.26 04.02.26 + 5 000,00 {RUBLE} + 5 000,00 {RUBLE} Salary"
            ])
        ]);

        Assert.Equal(parser.BankId, preview.BankId);
        Assert.Equal(accountId, preview.AccountId);
        Assert.Empty(preview.Errors);
        Assert.Collection(preview.Rows,
            row =>
            {
                Assert.Equal(new DateTime(2026, 2, 1, 13, 45, 0), row.OccurredAt);
                Assert.Equal(new DateTime(2026, 2, 2), row.ProcessedAt);
                Assert.Equal(1234.56m, row.Amount);
                Assert.Equal(MoneyMovementType.Expense, row.Type);
                Assert.Equal("Grocery", row.ImportComment);
                Assert.Equal(2, row.SourceLineNumber);
            },
            row =>
            {
                Assert.Equal(new DateTime(2026, 2, 3), row.OccurredAt);
                Assert.Equal(new DateTime(2026, 2, 4), row.ProcessedAt);
                Assert.Equal(5000m, row.Amount);
                Assert.Equal(MoneyMovementType.Income, row.Type);
                Assert.Equal("Salary", row.ImportComment);
            });
    }

    [Fact]
    public void Parse_JoinsContinuationLinesIntoImportComment()
    {
        IBankStatementParser parser = CreateParser();

        BankStatementImportPreviewDto preview = parser.Parse(Guid.NewGuid(),
        [
            string.Join('\n',
            [
                CARD_BLOCK_HEADER,
                $"05.02.26 12:00 06.02.26 -99,90 {RUBLE} -99,90 {RUBLE} Online",
                "subscription",
                $"07.02.26 08.02.26 -10,00 {RUBLE} -10,00 {RUBLE} Next"
            ])
        ]);

        Assert.Equal("Online subscription", preview.Rows[0].ImportComment);
        Assert.Equal("Next", preview.Rows[1].ImportComment);
    }

    [Fact]
    public void Parse_SkipsLinesBeforeCardBlockAndGarbageRows()
    {
        IBankStatementParser parser = CreateParser();

        BankStatementImportPreviewDto preview = parser.Parse(Guid.NewGuid(),
        [
            string.Join('\n',
            [
                $"01.02.26 02.02.26 -1,00 {RUBLE} -1,00 {RUBLE} Before block",
                "random text",
                CARD_BLOCK_HEADER,
                "not an operation",
                $"02.02.26 03.02.26 -2,00 {RUBLE} -2,00 {RUBLE} Parsed"
            ])
        ]);

        Assert.Single(preview.Rows);
        Assert.Equal("Parsed", preview.Rows[0].ImportComment);
        Assert.Empty(preview.Errors);
    }

    [Fact]
    public void Parse_AddsErrorWhenSplitOperationMissesAmount()
    {
        IBankStatementParser parser = CreateParser();

        BankStatementImportPreviewDto preview = parser.Parse(Guid.NewGuid(),
        [
            string.Join('\n',
            [
                CARD_BLOCK_HEADER,
                "09.02.26 10:15",
                "10.02.26",
                "missing amount"
            ])
        ]);

        Assert.Empty(preview.Rows);
        BankStatementImportLineErrorDto error = preview.Errors.Single(item => item.LineNumber == 2);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal("09.02.26 10:15", error.RawText);
    }

    private static IBankStatementParser CreateParser()
    {
        Type parserType = typeof(IBankStatementParser).Assembly.GetType(
            "CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsers.TBank.TBankPdfStatementParser",
            true)!;

        return (IBankStatementParser)Activator.CreateInstance(parserType, nonPublic: true)!;
    }
}
