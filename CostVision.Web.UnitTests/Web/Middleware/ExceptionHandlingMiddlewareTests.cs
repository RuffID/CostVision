using System.Text.Json;
using CostVision.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Web.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsUnifiedJsonContract_ForUnexpectedException()
    {
        ExceptionHandlingMiddleware middleware = new(new Mock<ILogger<ExceptionHandlingMiddleware>>().Object);
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("Unexpected"));

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("Внутренняя ошибка сервера.", root.GetProperty("message").GetString());
        Assert.False(root.TryGetProperty("data", out _));
        Assert.False(root.TryGetProperty("error", out _));
    }
}
