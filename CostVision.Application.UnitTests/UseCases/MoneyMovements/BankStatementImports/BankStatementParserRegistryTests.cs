using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements.BankStatementImports;

public class BankStatementParserRegistryTests
{
    [Fact]
    public void FindByBankId_ReturnsParserIgnoringCase()
    {
        IBankStatementParser tBankParser = CreateParser("tbank").Object;
        IBankStatementParser sberParser = CreateParser("sber").Object;
        BankStatementParserRegistry registry = new([tBankParser, sberParser]);

        IBankStatementParser? parser = registry.FindByBankId("SBER");

        Assert.Same(sberParser, parser);
    }

    [Fact]
    public void FindByBankId_ReturnsNullForUnknownBank()
    {
        BankStatementParserRegistry registry = new([CreateParser("tbank").Object]);

        IBankStatementParser? parser = registry.FindByBankId("unknown");

        Assert.Null(parser);
    }

    [Fact]
    public void GetParsers_ReturnsSupportedParsersInRegistrationOrder()
    {
        IBankStatementParser first = CreateParser("first").Object;
        IBankStatementParser second = CreateParser("second").Object;
        BankStatementParserRegistry registry = new([first, second]);

        IReadOnlyList<IBankStatementParser> parsers = registry.GetParsers();

        Assert.Equal([first, second], parsers);
    }

    private static Mock<IBankStatementParser> CreateParser(string bankId)
    {
        Mock<IBankStatementParser> parser = new(MockBehavior.Strict);
        parser.SetupGet(item => item.BankId).Returns(bankId);
        return parser;
    }
}
