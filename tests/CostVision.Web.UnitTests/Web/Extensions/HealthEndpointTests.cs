using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Web.Extensions;

public class HealthEndpointTests
{
    [Theory]
    [InlineData("CostVision.Web")]
    [InlineData("another-host")]
    public async Task LibraryDefaults_MapAnonymousHealthWithStableProcessInformation(string applicationName)
    {
        // Arrange
        Mock<IHostEnvironment> environment = new();
        environment.SetupGet(item => item.ApplicationName).Returns(applicationName);
        ServiceCollection services = new();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton(environment.Object);
        services.AddUptimeHarborHealth();
        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        ApplicationBuilder application = new(provider);
        Mock<IEndpointRouteBuilder> routes = new();
        List<EndpointDataSource> sources = [];
        routes.SetupGet(item => item.ServiceProvider).Returns(provider);
        routes.SetupGet(item => item.DataSources).Returns(sources);
        routes.Setup(item => item.CreateApplicationBuilder()).Returns(application);

        // Act
        provider.GetRequiredService<IStartupValidator>().Validate();
        routes.Object.MapUptimeHarborHealth();
        RouteEndpoint endpoint = Assert.IsType<RouteEndpoint>(Assert.Single(Assert.Single(sources).Endpoints));
        using MemoryStream firstBody = new();
        using MemoryStream secondBody = new();
        DefaultHttpContext first = new() { RequestServices = provider };
        DefaultHttpContext second = new() { RequestServices = provider };
        first.Response.Body = firstBody;
        second.Response.Body = secondBody;
        await endpoint.RequestDelegate!(first);
        await endpoint.RequestDelegate(second);
        using JsonDocument json = JsonDocument.Parse(firstBody.ToArray());

        // Assert
        Assert.Equal("/health", endpoint.RoutePattern.RawText);
        Assert.Equal(["GET"], endpoint.Metadata.GetRequiredMetadata<IHttpMethodMetadata>().HttpMethods);
        Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.False(first.User.Identity?.IsAuthenticated ?? false);
        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", first.Response.ContentType);
        Assert.Equal("no-store", first.Response.Headers.CacheControl);
        Assert.Equal(["name", "startedAtUtc"], json.RootElement.EnumerateObject().Select(item => item.Name));
        Assert.Equal(applicationName, json.RootElement.GetProperty("name").GetString());
        string startedAtUtc = Assert.IsType<string>(json.RootElement.GetProperty("startedAtUtc").GetString());
        Assert.EndsWith("+00:00", startedAtUtc);
        Assert.True(DateTimeOffset.Parse(startedAtUtc, CultureInfo.InvariantCulture) <= DateTimeOffset.UtcNow);
        Assert.Equal(firstBody.ToArray(), secondBody.ToArray());
    }
}
