using System.Text;
using CostVision.Web.Extensions;
using CostVision.Web.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CostVision.Web.UnitTests.Web.Extensions;

public class WebConfigurationProviderTests
{
    [Fact]
    public void ActualConfiguration_UsesContentRoot_EnvironmentOverridesJson_CommandLineOverridesEnvironment()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), "CostVision-configuration-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, "Config");
        string path = Path.Combine(directory, "config.json");
        string key = "CostVisionConfigurationTest" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{\"" + key + "\":\"file\"}", Encoding.UTF8);
        Environment.SetEnvironmentVariable(key, "environment");
        try
        {
            // Act
            IConfigurationRoot fromEnvironment = new ConfigurationBuilder().AddCostVisionConfiguration(root, []).Build();
            IConfigurationRoot fromCommandLine = new ConfigurationBuilder()
                .AddCostVisionConfiguration(root, ["--" + key + "=command"]).Build();

            // Assert
            Assert.Equal("environment", fromEnvironment[key]);
            Assert.Equal("command", fromCommandLine[key]);
            Assert.NotEqual(Path.GetFullPath("."), root);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
            File.Delete(path);
            Directory.Delete(directory);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void ActualConfiguration_RequiresFileUnderContentRoot()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), "CostVision-configuration-missing-" + Guid.NewGuid().ToString("N"));

        // Act
        Assert.Throws<FileNotFoundException>(() => new ConfigurationBuilder().AddCostVisionConfiguration(root, []).Build());

        // Assert
        Assert.False(Directory.Exists(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualWebOptions_KeepDefaultsOrResolveExplicitKeyRingWithoutCreatingDirectories(bool configured)
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), "CostVision-configuration-" + Guid.NewGuid().ToString("N"));
        Dictionary<string, string?> values = new();
        if (configured)
        {
            values["DataProtection:KeyDirectory"] = "shared-keys";
            values["DataProtection:ApplicationName"] = "test-app";
            values["Swagger:Enabled"] = "false";
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        ServiceCollection registrations = new();
        registrations.AddLogging();
        registrations.AddWebRuntimeConfiguration(configuration, root);
        using ServiceProvider services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        // Act
        services.GetRequiredService<IStartupValidator>().Validate();
        DataProtectionSettings settings = services.GetRequiredService<IOptions<DataProtectionSettings>>().Value;
        DataProtectionOptions protection = services.GetRequiredService<IOptions<DataProtectionOptions>>().Value;
        FileSystemXmlRepository repository = Assert.IsType<FileSystemXmlRepository>(services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository);
        long? requestLimit = services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize;
        bool swaggerEnabled = services.GetRequiredService<IOptions<SwaggerSettings>>().Value.Enabled;

        // Assert
        string directory = "keys-linux";
        string applicationName = "CostVision.Web";
        if (OperatingSystem.IsWindows())
        {
            directory = "keys-windows";
        }

        if (configured)
        {
            directory = "shared-keys";
            applicationName = "test-app";
        }

        Assert.Equal(Path.Combine(root, directory), settings.KeyDirectory);
        Assert.Equal(settings.KeyDirectory, repository.Directory.FullName);
        Assert.Equal(applicationName, protection.ApplicationDiscriminator);
        Assert.Equal(1_000_000_000, requestLimit);
        Assert.Equal(!configured, swaggerEnabled);
        Assert.False(Directory.Exists(root));
    }
}
