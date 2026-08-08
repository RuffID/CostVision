using System.Net;
using System.Text.Json;
using CostVision.Application.Models.Responses.Results;
using CostVision.Infrastructure.IntegrationTests.Web.Helpers;
using CostVision.Infrastructure.IntegrationTests.Web.Models;
using CostVision.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.Web;

public class WebAuthorizationIntegrationTests : IDisposable
{
    private readonly TestWebApplication _app = new();

    [Fact]
    public async Task Login_WithValidCredentials_SetsAuthenticationCookieAndClaims()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AntiforgeryContext antiforgery = await _app.GetAntiforgeryContextAsync();

        HttpResponseMessage loginResponse = await _app.PostLoginAsync(
            antiforgery,
            "active",
            "password");

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Contains(loginResponse.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".CostVision.Cookies=", StringComparison.Ordinal));

        string cookieHeader = CookieHeader.Merge(antiforgery.CookieHeader, loginResponse);
        using HttpRequestMessage authProbeRequest = new(HttpMethod.Get, "/test-auth");
        authProbeRequest.Headers.Add("Cookie", cookieHeader);

        HttpResponseMessage authProbeResponse = await _app.Client.SendAsync(authProbeRequest, ct);

        Assert.Equal(HttpStatusCode.OK, authProbeResponse.StatusCode);

        await using Stream stream = await authProbeResponse.Content.ReadAsStreamAsync(ct);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        JsonElement root = json.RootElement;

        Assert.True(root.GetProperty("authenticated").GetBoolean());
        Assert.Equal(_app.ActiveUser.Id.ToString(), root.GetProperty("nameIdentifier").GetString());
        Assert.Equal("Integration User", root.GetProperty("name").GetString());
        Assert.Contains("Admin", root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("blocked")]
    public async Task Login_WithRejectedCredentials_DoesNotSetAuthenticationCookie(string login)
    {
        AntiforgeryContext antiforgery = await _app.GetAntiforgeryContextAsync();

        HttpResponseMessage loginResponse = await _app.PostLoginAsync(
            antiforgery,
            login,
            "password");

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.False(loginResponse.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
            && cookies.Any(value => value.StartsWith(".CostVision.Cookies=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ProtectedPage_WithoutAuthentication_RedirectsToLogin()
    {
        HttpResponseMessage response = await _app.Client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AjaxEndpoint_WithSuccessfulServiceResult_ReturnsOkJson()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticatedContext context = await _app.LoginAsync();
        _app.MarkUserActivity.Result = ServiceResult.Ok();

        HttpResponseMessage response = await _app.PostUserActivityPingAsync(context);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        JsonElement root = json.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(root.TryGetProperty("data", out _));
        Assert.Equal(_app.ActiveUser.Id, _app.MarkUserActivity.LastUserId);
    }

    [Fact]
    public async Task AjaxEndpoint_WithFailedServiceResult_ReturnsMappedStatusAndJson()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        AuthenticatedContext context = await _app.LoginAsync();
        _app.MarkUserActivity.Result = ServiceResult.Fail(ServiceErrorType.Forbidden, "Denied");

        HttpResponseMessage response = await _app.PostUserActivityPingAsync(context);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        JsonElement root = json.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal("Denied", root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_MapsUnhandledExceptionAndLogsIt()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ListLoggerProvider loggerProvider = new();
        using IHost host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddLogging(builder => builder.AddProvider(loggerProvider));
                    services.AddTransient<ExceptionHandlingMiddleware>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.Run(_ => throw new InvalidOperationException("Pipeline failure"));
                });
            })
            .StartAsync(ct);

        HttpResponseMessage response = await host.GetTestClient().GetAsync("/throws", ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(loggerProvider.Entries, entry => entry.LogLevel == LogLevel.Error
            && entry.Message.Contains("Unhandled exception.", StringComparison.Ordinal));

        await using Stream stream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Внутренняя ошибка сервера.", json.RootElement.GetProperty("message").GetString());
    }

    public void Dispose()
    {
        _app.Dispose();
    }
}
