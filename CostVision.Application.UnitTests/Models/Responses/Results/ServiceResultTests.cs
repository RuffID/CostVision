using CostVision.Application.Models.Responses.Results;
using Xunit;

namespace CostVision.Application.UnitTests.Models.Responses.Results;

public class ServiceResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulNonGenericResult()
    {
        ServiceResult result = ServiceResult.Ok();

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_CreatesFailureWithStatusAndMessage()
    {
        ServiceResult result = ServiceResult.Fail(404, "Not found");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(404, result.Error.StatusCode);
        Assert.Equal("Not found", result.Error.Message);
    }

    [Fact]
    public void GenericOk_CreatesSuccessfulResultWithPayload()
    {
        var payload = new { Name = "payload" };

        ServiceResult<object> result = ServiceResult<object>.Ok(payload);

        Assert.True(result.Success);
        Assert.Same(payload, result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericFail_CreatesFailureWithDefaultPayload()
    {
        ServiceResult<string> result = ServiceResult<string>.Fail(400, "Bad request");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.NotNull(result.Error);
        Assert.Equal(400, result.Error.StatusCode);
        Assert.Equal("Bad request", result.Error.Message);
    }
}
