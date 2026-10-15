using CostVision.Application.Extensions;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Extensions;
using CostVision.Infrastructure.Services.DataBase;
using CostVision.Web.Extensions;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Web.Extensions;

public class StartupOptionsValidationTests
{
    [Theory]
    [InlineData("ApiEndpoints:ProverkachekaApiUrl", "ftp://test-only-secret.invalid/")]
    [InlineData("ProverkachekaApiToken", "")]
    [InlineData("ExternalRequests:TimeoutSeconds", "NaN")]
    [InlineData("BankStatementPdf:MaxBytes", "0")]
    [InlineData("Uploads:MaxRequestBodyBytes", "0")]
    [InlineData("DataProtection:ApplicationName", " ")]
    [InlineData("ForwardedHeaders:KnownProxies:0", "invalid-test-only-secret")]
    [InlineData("DatabaseMaintenance:SingleInitializerConfirmed", "false")]
    public async Task ActualStartupBoundary_ValidatesAllOptionsBeforeMaintenance_LogsOnlyCode(string key, string value)
    {
        // Arrange
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        Mock<ILogger> logger = new();
        Mock<ILoggerFactory> loggers = new();
        loggers.Setup(item => item.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        await using ServiceProvider services = CreateServices(maintenance, key, value, loggers.Object);

        // Act
        bool ready = await WebApplicationExtensions.InitializeDatabaseServicesAsync(services, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(ready);
        Assert.False(services.GetRequiredService<DatabaseMaintenanceStartup>().IsReady);
        maintenance.VerifyNoOtherCalls();
        var entry = Assert.Single(logger.Invocations, item => item.Method.Name == "Log");
        string text = Assert.IsType<string>(entry.Arguments[2]?.ToString());
        Assert.Equal("Application startup failed: Configuration.", text);
        Assert.Null(entry.Arguments[3]);
    }

    [Fact]
    public async Task ActualStartupBoundary_PassesFiniteBudgetAndWaitsForCleanupBeforeReadiness()
    {
        // Arrange
        TaskCompletionSource cleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cleanupRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        maintenance.Setup(item => item.UpdateExistingAsync(TimeSpan.FromSeconds(17), TestContext.Current.CancellationToken))
            .ReturnsAsync(new DatabaseMaintenanceResult(MaintenanceOutcome.Unchanged, [], null));
        maintenance.As<IAsyncDisposable>().Setup(item => item.DisposeAsync()).Returns(async () =>
        {
            cleanupStarted.SetResult();
            await cleanupRelease.Task;
        });
        await using ServiceProvider services = CreateServices(maintenance);

        // Act
        Task<bool> operation = WebApplicationExtensions.InitializeDatabaseServicesAsync(services, TestContext.Current.CancellationToken);
        await cleanupStarted.Task;

        // Assert
        Assert.False(operation.IsCompleted);
        Assert.False(services.GetRequiredService<DatabaseMaintenanceStartup>().IsReady);

        // Act
        cleanupRelease.SetResult();
        bool ready = await operation;

        // Assert
        Assert.True(ready);
        Assert.True(services.GetRequiredService<DatabaseMaintenanceStartup>().IsReady);
        maintenance.VerifyAll();
        Assert.Equal(2, maintenance.Invocations.Count);
    }

    private static ServiceProvider CreateServices(Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance,
        string? key = null, string? value = null, ILoggerFactory? loggerFactory = null)
    {
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:MSSql"] = "Server=unused;Database=unused;ConnectRetryCount=0",
            ["ApiEndpoints:ProverkachekaApiUrl"] = "https://unused.test/receipt",
            ["ProverkachekaApiToken"] = "test-only-secret",
            ["DatabaseMaintenance:ServerBackupDirectory"] = "/test/server-only/backups",
            ["DatabaseMaintenance:SingleInitializerConfirmed"] = "true",
            ["DatabaseMaintenance:TimeoutSeconds"] = "17",
            ["ForwardedHeaders:KnownProxies:0"] = "127.0.0.1"
        };
        if (key != null)
        {
            values[key] = value;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddWebRuntimeConfiguration(configuration, Path.GetFullPath("."));
        services.AddForwardedHeadersConfiguration(configuration);
        services.AddScoped(_ => maintenance.Object);
        if (loggerFactory != null)
        {
            services.AddSingleton(loggerFactory);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
