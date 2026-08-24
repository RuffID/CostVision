using CostVision.Application.Models.Responses.Results;
using Xunit;

namespace CostVision.Application.UnitTests.Models.Responses.Results;

public class ServiceResultTests
{
    [Fact]
    public void Ok_CreatesPayloadlessSuccessWithoutError()
    {
        ServiceResult result = ServiceResult.Ok();

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(ServiceErrorType.Validation)]
    [InlineData(ServiceErrorType.Unauthorized)]
    [InlineData(ServiceErrorType.Forbidden)]
    [InlineData(ServiceErrorType.NotFound)]
    [InlineData(ServiceErrorType.Conflict)]
    [InlineData(ServiceErrorType.ExternalService)]
    public void Fail_PreservesSemanticErrorWithoutPayload(ServiceErrorType errorType)
    {
        ServiceResult<string> result = ServiceResult<string>.Fail(errorType, "Error");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal(errorType, result.Error?.Type);
        Assert.Equal("Error", result.Error?.Message);
    }

    [Fact]
    public void GenericOk_CreatesSuccessWithRequiredPayloadAndWithoutError()
    {
        object payload = new();

        ServiceResult<object> result = ServiceResult<object>.Ok(payload);

        Assert.True(result.Success);
        Assert.Same(payload, result.Data);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericOk_RejectsNullPayload()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceResult<string>.Ok(null!));
    }

    [Fact]
    public void PropagateFailure_TransfersSameErrorAcrossResultTypes()
    {
        ServiceError error = new(ServiceErrorType.Conflict, "Conflict");
        ServiceResult<int> source = ServiceResult<int>.Fail(error);

        ServiceResult<string> target = source.PropagateFailure<string>();

        Assert.False(target.Success);
        Assert.Same(error, target.Error);
        Assert.Null(target.Data);
    }

    [Fact]
    public void PropagateFailure_RejectsSuccessfulResult()
    {
        ServiceResult<int> result = ServiceResult<int>.Ok(1);

        Assert.Throws<InvalidOperationException>(() => result.PropagateFailure<string>());
    }

    [Fact]
    public void ServiceError_RejectsBlankMessage()
    {
        Assert.Throws<ArgumentException>(() => new ServiceError(ServiceErrorType.Validation, " "));
    }
}
