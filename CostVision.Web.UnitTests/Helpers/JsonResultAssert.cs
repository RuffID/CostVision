using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CostVision.Web.UnitTests.Helpers;

internal static class JsonResultAssert
{
    public static T Data<T>(JsonResult json)
    {
        Assert.Equal(200, json.StatusCode);
        Assert.True(Value<bool>(json, "success"));
        return Value<T>(json, "data");
    }

    public static void Success(JsonResult json)
    {
        Assert.Equal(200, json.StatusCode);
        Assert.True(Value<bool>(json, "success"));
    }

    public static void Failure(JsonResult json, int statusCode, string message)
    {
        Assert.Equal(statusCode, json.StatusCode);
        Assert.False(Value<bool>(json, "success"));
        Assert.Equal(message, Value<string>(json, "message"));
    }

    private static T Value<T>(JsonResult json, string propertyName)
    {
        return (T)json.Value!.GetType().GetProperty(propertyName)!.GetValue(json.Value)!;
    }
}
