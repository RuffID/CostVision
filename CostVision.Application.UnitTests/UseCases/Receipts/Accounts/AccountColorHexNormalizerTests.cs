using System.Reflection;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Receipts;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class AccountColorHexNormalizerTests
{
    [Theory]
    [InlineData("#abcdef", "#ABCDEF")]
    [InlineData(" #123ABC ", "#123ABC")]
    public void Normalize_ReturnsUppercaseHex_WhenValueIsValid(string value, string expected)
    {
        ServiceResult<string> result = Normalize(value);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Data);
    }

    [Fact]
    public void Normalize_ReturnsDefaultColor_WhenValueIsEmpty()
    {
        ServiceResult<string> result = Normalize(" ");

        Assert.True(result.Success);
        Assert.Equal(Account.DEFAULT_COLOR_HEX, result.Data);
    }

    [Theory]
    [InlineData("ABCDEF")]
    [InlineData("#12")]
    [InlineData("red")]
    public void Normalize_ReturnsBadRequest_WhenValueIsInvalid(string value)
    {
        ServiceResult<string> result = Normalize(value);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    private static ServiceResult<string> Normalize(string? value)
    {
        Type type = typeof(CostVision.Application.UseCases.Receipts.Accounts.CreateAccountUseCase).Assembly
            .GetType("CostVision.Application.UseCases.Receipts.Accounts.Helpers.AccountColorHexNormalizer")!;
        MethodInfo method = type.GetMethod("Normalize", BindingFlags.Public | BindingFlags.Static)!;
        return (ServiceResult<string>)method.Invoke(null, [value])!;
    }
}
