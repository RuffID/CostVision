using System.Text.Json;
using CostVision.Web.Extensions;
using CostVision.Web.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests;

public class AuthenticationAndHttpErrorResponseTests
{
    [Theory]
    [InlineData(false, 401)]
    [InlineData(true, 403)]
    public async Task CookieEvents_ReturnJsonStatusForAjax(bool forbidden, int expectedStatus)
    {
        // Arrange
        CookieAuthenticationOptions options = new();
        CookieHttpContract.Configure(options);
        DefaultHttpContext context = CreateContext();
        context.Request.Headers.Accept = "application/json";
        RedirectContext<CookieAuthenticationOptions> redirect = CreateRedirect(context, options);

        // Act
        if (forbidden)
        {
            await options.Events.OnRedirectToAccessDenied(redirect);
        }
        else
        {
            await options.Events.OnRedirectToLogin(redirect);
        }

        // Assert
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        JsonElement json = ReadJson(context);
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.NotEmpty(json.GetProperty("message").GetString()!);
        Assert.Equal(".CostVision.Cookies", options.Cookie.Name);
        Assert.Equal(TimeSpan.FromDays(14), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CookieEvents_PreserveHtmlRedirect(bool forbidden)
    {
        // Arrange
        CookieAuthenticationOptions options = new();
        CookieHttpContract.Configure(options);
        DefaultHttpContext context = CreateContext();
        context.Request.Headers.Accept = "text/html";
        RedirectContext<CookieAuthenticationOptions> redirect = CreateRedirect(context, options);

        // Act
        if (forbidden)
        {
            await options.Events.OnRedirectToAccessDenied(redirect);
        }
        else
        {
            await options.Events.OnRedirectToLogin(redirect);
        }

        // Assert
        Assert.Equal(302, context.Response.StatusCode);
        Assert.Equal("/login?ReturnUrl=%2Freceipts", context.Response.Headers.Location);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Theory]
    [InlineData(false, false, 500)]
    [InlineData(false, true, 504)]
    [InlineData(true, false, 499)]
    public async Task Middleware_DistinguishesInternalCancellationTimeoutAndRequestAbort(bool aborted, bool timeout, int expectedStatus)
    {
        // Arrange
        ExceptionHandlingMiddleware middleware = new(NullLogger<ExceptionHandlingMiddleware>.Instance);
        DefaultHttpContext context = CreateContext();
        using CancellationTokenSource cancellation = new();
        if (aborted)
        {
            cancellation.Cancel();
            context.RequestAborted = cancellation.Token;
        }

        Exception failure = new OperationCanceledException("Internal secret");
        if (timeout)
        {
            failure = new TimeoutException("Internal secret");
        }

        // Act
        await middleware.InvokeAsync(context, _ => throw failure);

        // Assert
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        if (aborted)
        {
            Assert.Equal(0, context.Response.Body.Length);
        }
        else
        {
            JsonElement json = ReadJson(context);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.DoesNotContain("secret", json.ToString());
        }
    }

    [Fact]
    public async Task StartedResponse_RethrowsWithoutSecondWrite()
    {
        // Arrange
        ExceptionHandlingMiddleware middleware = new(NullLogger<ExceptionHandlingMiddleware>.Instance);
        DefaultHttpContext context = CreateContext();
        Mock<IHttpResponseFeature> feature = new(MockBehavior.Strict);
        feature.SetupGet(item => item.HasStarted).Returns(true);
        context.Features.Set(feature.Object);
        InvalidOperationException failure = new("secret");

        // Act
        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(context, _ => throw failure));

        // Assert
        Assert.Same(failure, thrown);
        feature.VerifyGet(item => item.HasStarted, Times.Once);
        feature.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(400)]
    [InlineData(413)]
    public async Task BadHttpRequest_PreservesStatusAndClearsPreviousResponse(int expectedStatus)
    {
        // Arrange
        ExceptionHandlingMiddleware middleware = new(NullLogger<ExceptionHandlingMiddleware>.Instance);
        DefaultHttpContext context = CreateContext();
        context.Request.Headers.Accept = "application/json";
        context.Response.Headers.Location = "/previous";
        BadHttpRequestException failure = new("Internal secret", expectedStatus);

        // Act
        await middleware.InvokeAsync(context, _ => throw failure);

        // Assert
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        JsonElement json = ReadJson(context);
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.NotEmpty(json.GetProperty("message").GetString()!);
        Assert.DoesNotContain("secret", json.ToString());
    }

    [Fact]
    public async Task HtmlFailure_RemainsAnErrorWithoutDiagnostics()
    {
        // Arrange
        ExceptionHandlingMiddleware middleware = new(NullLogger<ExceptionHandlingMiddleware>.Instance);
        DefaultHttpContext context = CreateContext();
        context.Request.Headers.Accept = "text/html";

        // Act
        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("secret"));

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", context.Response.ContentType);
        context.Response.Body.Position = 0;
        string text = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Внутренняя ошибка сервера.", text);
        Assert.DoesNotContain("secret", text);
    }

    private static DefaultHttpContext CreateContext()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task StartedClientCancellation_DoesNotTouchResponse()
    {
        // Arrange
        ExceptionHandlingMiddleware middleware = new(NullLogger<ExceptionHandlingMiddleware>.Instance);
        DefaultHttpContext context = CreateContext();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        context.RequestAborted = cancellation.Token;
        Mock<IHttpResponseFeature> feature = new(MockBehavior.Strict);
        feature.SetupGet(item => item.HasStarted).Returns(true);
        context.Features.Set(feature.Object);

        // Act
        await middleware.InvokeAsync(context, _ => throw new OperationCanceledException(cancellation.Token));

        // Assert
        feature.VerifyGet(item => item.HasStarted, Times.Once);
        feature.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UnknownErrorAfterClientAbort_DoesNotWriteToClosedRequest()
    {
        // Arrange
        ExceptionHandlingMiddleware middleware = new(NullLogger<ExceptionHandlingMiddleware>.Instance);
        DefaultHttpContext context = CreateContext();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        context.RequestAborted = cancellation.Token;

        // Act
        await middleware.InvokeAsync(context, _ => throw new InvalidOperationException("secret"));

        // Assert
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateRedirect(HttpContext context, CookieAuthenticationOptions options)
    {
        AuthenticationScheme scheme = new("Cookies", "Cookies", typeof(CookieAuthenticationHandler));
        return new RedirectContext<CookieAuthenticationOptions>(context, scheme, options, new AuthenticationProperties(),
            "/login?ReturnUrl=%2Freceipts");
    }

    private static JsonElement ReadJson(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using JsonDocument json = JsonDocument.Parse(context.Response.Body);
        return json.RootElement.Clone();
    }
}
