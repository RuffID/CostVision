using System.Net;
using CostVision.Web.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CostVision.Web.UnitTests.Web.Extensions;

public class ForwardedHeadersServiceCollectionExtensionsTests
{
    [Fact]
    public void AddForwardedHeadersConfiguration_ConfiguresHeadersAndKnownProxy()
    {
        Dictionary<string, string?> values = new()
        {
            ["ForwardedHeaders:KnownProxies:0"] = "172.19.0.1"
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        ServiceCollection services = new();

        services.AddForwardedHeadersConfiguration(configuration);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        ForwardedHeadersOptions options = serviceProvider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        ForwardedHeaders expectedHeaders = ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedProto
            | ForwardedHeaders.XForwardedHost;

        Assert.Equal(expectedHeaders, options.ForwardedHeaders);
        Assert.Contains(IPAddress.Parse("172.19.0.1"), options.KnownProxies);
    }
}
