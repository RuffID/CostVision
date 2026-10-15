using CostVision.Application.Extensions;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Extensions;
using CostVision.Infrastructure.Models.ConfigClass;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.SqlServer.Providers;
using HttpClientLibrary.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CostVision.Infrastructure.UnitTests;

public class InfrastructureOptionsValidationTests
{
    [Theory]
    [InlineData("ApiEndpoints:ProverkachekaApiUrl", "ftp://unused.test/receipt")]
    [InlineData("ApiEndpoints:ProverkachekaApiUrl", "file:///private/test")]
    [InlineData("ApiEndpoints:ProverkachekaApiUrl", "/relative")]
    [InlineData("ProverkachekaApiToken", " ")]
    [InlineData("ExternalRequests:TimeoutSeconds", "0")]
    [InlineData("ExternalRequests:TimeoutSeconds", "-1")]
    [InlineData("ExternalRequests:TimeoutSeconds", "NaN")]
    [InlineData("ExternalRequests:TimeoutSeconds", "Infinity")]
    [InlineData("ExternalRequests:TimeoutSeconds", "2147484")]
    [InlineData("ExternalRequests:TimeoutSeconds", "0.000000001")]
    [InlineData("BankStatementPdf:MaxBytes", "0")]
    [InlineData("BankStatementPdf:MaxPages", "-1")]
    [InlineData("DatabaseMaintenance:SingleInitializerConfirmed", "false")]
    [InlineData("DatabaseMaintenance:ServerBackupDirectory", " ")]
    [InlineData("DatabaseMaintenance:TimeoutSeconds", "0")]
    public void ActualStartupValidator_RejectsInvalidOptionsBeforeAnyIo(string key, string value)
    {
        // Arrange
        Dictionary<string, string?> values = ValidConfiguration();
        values[key] = value;
        using ServiceProvider services = CreateServices(values);

        // Act
        OptionsValidationException error = Assert.Throws<OptionsValidationException>(
            () => services.GetRequiredService<IStartupValidator>().Validate());

        // Assert
        Assert.NotEmpty(error.Failures);
        Assert.DoesNotContain("test-token", error.Message);
        Assert.DoesNotContain("private/test", error.Message);
        using IServiceScope scope = services.CreateScope();
        ApplicationContext context = scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void ActualOptions_ConfigureTypedClientAndKeepMaintenanceSnapshot()
    {
        // Arrange
        Dictionary<string, string?> values = ValidConfiguration();
        values["ExternalRequests:TimeoutSeconds"] = "12.5";
        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using ServiceProvider services = CreateServices(configuration);
        configuration["DatabaseMaintenance:ServerBackupDirectory"] = "/changed/after/registration";
        configuration["DatabaseMaintenance:Operation"] = "InitializeNew";

        // Act
        services.GetRequiredService<IStartupValidator>().Validate();
        using HttpClient client = services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IHttpApiClient));
        DatabaseMaintenanceSettings startup = services.GetRequiredService<IOptions<DatabaseMaintenanceSettings>>().Value;
        using IServiceScope scope = services.CreateScope();
        IDatabaseMaintenanceProvider<CostVisionContextKey> library = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<CostVisionContextKey>>();
        BankStatementPdfSettings pdf = services.GetRequiredService<IOptions<BankStatementPdfSettings>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(12.5), client.Timeout);
        Assert.Equal("/test/server-only/backups", startup.ServerBackupDirectory);
        Assert.IsType<SqlServerMaintenanceProvider<CostVisionContextKey>>(library);
        Assert.Equal(DatabaseStartupOperation.UpdateExisting, startup.Operation);
        Assert.Equal(20 * 1024 * 1024, pdf.MaxBytes);
        Assert.Equal(200, pdf.MaxPages);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingConnection_IsRejectedDuringRegistrationWithoutExposingValues(string? connection)
    {
        // Arrange
        Dictionary<string, string?> values = ValidConfiguration();
        values["ConnectionStrings:MSSql"] = connection;
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        ServiceCollection services = new();

        // Act
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        // Assert
        Assert.Contains("ConnectionStrings:MSSql", error.Message);
        Assert.DoesNotContain("test-token", error.Message);
    }

    private static ServiceProvider CreateServices(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return CreateServices(configuration);
    }

    private static ServiceProvider CreateServices(IConfiguration configuration)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static Dictionary<string, string?> ValidConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:MSSql"] = "Server=unused;Database=unused;ConnectRetryCount=0",
            ["ApiEndpoints:ProverkachekaApiUrl"] = "https://unused.test/receipt",
            ["ProverkachekaApiToken"] = "test-token",
            ["DatabaseMaintenance:ServerBackupDirectory"] = "/test/server-only/backups",
            ["DatabaseMaintenance:SingleInitializerConfirmed"] = "true"
        };
    }
}
