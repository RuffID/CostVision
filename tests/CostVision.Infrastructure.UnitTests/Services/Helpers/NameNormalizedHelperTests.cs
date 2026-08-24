using CostVision.Infrastructure.Services.Helpers;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Helpers;

public class NameNormalizedHelperTests
{
    [Fact]
    public void GetNormalizedName_ConvertsToUpperCase()
    {
        string result = NameNormalizedHelper.GetNormalizedName("milk");

        Assert.Equal("MILK", result);
    }

    [Fact]
    public void GetNormalizedName_RemovesExtraSeparators()
    {
        string result = NameNormalizedHelper.GetNormalizedName("  milk chocolate-20 / 30,5.  ");

        Assert.Equal("MILKCHOCOLATE20305", result);
    }

    [Fact]
    public void GetNormalizedName_NormalizesRussianYoLetter()
    {
        string result = NameNormalizedHelper.GetNormalizedName("  мёд  ");

        Assert.Equal("МЕД", result);
    }

    [Fact]
    public void GetNormalizedName_ReturnsEmptyStringForEmptyInput()
    {
        string result = NameNormalizedHelper.GetNormalizedName("   ");

        Assert.Equal(string.Empty, result);
    }
}
