using CostVision.Application.Models.Responses.Results;
using CostVision.Web.Mappers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CostVision.Web.UnitTests.Web.Mappers;

public class JsonResultMapperTests
{
    public static TheoryData<ServiceErrorType, int> ErrorMappings => new()
    {
        { ServiceErrorType.Validation, StatusCodes.Status400BadRequest },
        { ServiceErrorType.Unauthorized, StatusCodes.Status401Unauthorized },
        { ServiceErrorType.Forbidden, StatusCodes.Status403Forbidden },
        { ServiceErrorType.NotFound, StatusCodes.Status404NotFound },
        { ServiceErrorType.Conflict, StatusCodes.Status409Conflict },
        { ServiceErrorType.ExternalService, StatusCodes.Status502BadGateway }
    };

    [Theory]
    [MemberData(nameof(ErrorMappings))]
    public void ToJsonResult_MapsEveryErrorType(ServiceErrorType errorType, int expectedStatusCode)
    {
        JsonResult json = JsonResultMapper.ToJsonResult(ServiceResult.Fail(errorType, "Error"));

        Assert.Equal(expectedStatusCode, json.StatusCode);
        Assert.False(Value<bool>(json, "success"));
        Assert.Equal("Error", Value<string>(json, "message"));
        Assert.Null(json.Value!.GetType().GetProperty("data"));
        Assert.Null(json.Value.GetType().GetProperty("error"));
    }

    [Fact]
    public void ToJsonResult_MapsPayloadlessSuccessWithoutErrorOrData()
    {
        JsonResult json = JsonResultMapper.ToJsonResult(ServiceResult.Ok());

        Assert.Equal(StatusCodes.Status200OK, json.StatusCode);
        Assert.True(Value<bool>(json, "success"));
        Assert.Null(json.Value!.GetType().GetProperty("data"));
        Assert.Null(json.Value.GetType().GetProperty("message"));
        Assert.Null(json.Value.GetType().GetProperty("error"));
    }

    [Fact]
    public void ToJsonResult_MapsGenericSuccessWithRequiredDataAndWithoutError()
    {
        object payload = new();

        JsonResult json = JsonResultMapper.ToJsonResult(ServiceResult<object>.Ok(payload));

        Assert.Equal(StatusCodes.Status200OK, json.StatusCode);
        Assert.True(Value<bool>(json, "success"));
        Assert.Same(payload, Value<object>(json, "data"));
        Assert.Null(json.Value!.GetType().GetProperty("message"));
        Assert.Null(json.Value.GetType().GetProperty("error"));
    }

    [Fact]
    public void ToJsonResult_RejectsUnknownErrorType()
    {
        ServiceResult result = ServiceResult.Fail((ServiceErrorType)int.MaxValue, "Unknown");

        Assert.Throws<ArgumentOutOfRangeException>(() => JsonResultMapper.ToJsonResult(result));
    }

    private static T Value<T>(JsonResult json, string propertyName)
    {
        return (T)json.Value!.GetType().GetProperty(propertyName)!.GetValue(json.Value)!;
    }
}
