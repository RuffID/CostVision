using CostVision.Application.Models.Dtos.Mappers;
using CostVision.Application.Models.Responses.Results;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CostVision.Application.UnitTests.Models.Dtos.Mappers;

public class JsonResultMapperTests
{
    [Fact]
    public void ToJsonResult_MapsSuccessfulNonGenericResult()
    {
        JsonResult json = JsonResultMapper.ToJsonResult(ServiceResult.Ok());

        Assert.Equal(200, json.StatusCode);
        Assert.True(GetValue<bool>(json, "success"));
    }

    [Fact]
    public void ToJsonResult_MapsFailureStatusAndMessage()
    {
        JsonResult json = JsonResultMapper.ToJsonResult(ServiceResult.Fail(403, "Forbidden"));

        Assert.Equal(403, json.StatusCode);
        Assert.False(GetValue<bool>(json, "success"));
        Assert.Equal("Forbidden", GetValue<string>(json, "message"));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(422)]
    public void ToJsonResult_MapsGenericFailureStatusCodes(int statusCode)
    {
        JsonResult json = JsonResultMapper.ToJsonResult(ServiceResult<string>.Fail(statusCode, "Error"));

        Assert.Equal(statusCode, json.StatusCode);
        Assert.False(GetValue<bool>(json, "success"));
        Assert.Equal("Error", GetValue<string>(json, "message"));
    }

    [Fact]
    public void ToJsonResult_MapsSuccessfulGenericPayload()
    {
        var payload = new { id = 7, name = "payload" };

        JsonResult json = JsonResultMapper.ToJsonResult(ServiceResult<object>.Ok(payload));

        Assert.Equal(200, json.StatusCode);
        Assert.True(GetValue<bool>(json, "success"));
        Assert.Same(payload, GetValue<object>(json, "data"));
    }

    private static T GetValue<T>(JsonResult json, string propertyName)
    {
        return (T)json.Value!.GetType().GetProperty(propertyName)!.GetValue(json.Value)!;
    }
}
