using System.Reflection;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements.BankStatementImports;

public class BankStatementTextNormalizerTests
{
    [Fact]
    public void NormalizePages_TrimsAndCollapsesWhitespace()
    {
        List<object> lines = Normalize(["  first\t\tline  \n second    line  "]);

        Assert.Equal(["first line", "second line"], lines.Select(GetText).ToList());
    }

    [Fact]
    public void NormalizePages_SupportsDifferentLineEndingsAndKeepsSourceLineNumbers()
    {
        List<object> lines = Normalize(["first\r\n\r\n third", "fourth\rfifth"]);

        Assert.Equal(["first", "third", "fourth", "fifth"], lines.Select(GetText).ToList());
        Assert.Equal([1, 3, 4, 5], lines.Select(GetNumber).ToList());
    }

    [Fact]
    public void NormalizePages_ReplacesNonBreakingSpacesAndIgnoresEmptyText()
    {
        List<object> lines = Normalize(["\u00a0alpha\u00a0\u00a0beta\u00a0\n   "]);

        object line = Assert.Single(lines);
        Assert.Equal("alpha beta", GetText(line));
        Assert.Equal(1, GetNumber(line));
    }

    private static List<object> Normalize(IReadOnlyList<string> pages)
    {
        Type normalizerType = typeof(CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing.IBankStatementParser)
            .Assembly
            .GetType("CostVision.Application.UseCases.MoneyMovements.BankStatementImports.Parsing.BankStatementTextNormalizer", true)!;
        MethodInfo method = normalizerType.GetMethod("NormalizePages", BindingFlags.Public | BindingFlags.Static)!;

        return ((IEnumerable<object>)method.Invoke(null, [pages])!).ToList();
    }

    private static string GetText(object line)
    {
        return (string)line.GetType().GetProperty("Text")!.GetValue(line)!;
    }

    private static int GetNumber(object line)
    {
        return (int)line.GetType().GetProperty("Number")!.GetValue(line)!;
    }
}
