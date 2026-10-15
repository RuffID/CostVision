using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Models.ConfigClass;
using CostVision.Infrastructure.Services.DataBase;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class DatabaseMaintenanceStartupTests
{
    [Theory]
    [InlineData(DatabaseStartupOperation.UpdateExisting)]
    [InlineData(DatabaseStartupOperation.InitializeNew)]
    public async Task Startup_UsesExplicitOperationTimeoutAndCallerToken(DatabaseStartupOperation operation)
    {
        // Arrange
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        DatabaseMaintenanceSettings settings = ValidSettings();
        settings.Operation = operation;
        DatabaseMaintenanceResult result = new(MaintenanceOutcome.Unchanged, [], null);
        CancellationToken token = TestContext.Current.CancellationToken;
        TimeSpan timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        if (operation == DatabaseStartupOperation.InitializeNew)
        {
            result = new(MaintenanceOutcome.Initialized, [], null);
            maintenance.Setup(item => item.InitializeNewAsync(timeout, token)).ReturnsAsync(result);
        }
        else
        {
            maintenance.Setup(item => item.UpdateExistingAsync(timeout, token)).ReturnsAsync(result);
        }

        await using ServiceProvider root = CreateRoot(maintenance, settings);
        DatabaseMaintenanceStartup startup = root.GetRequiredService<DatabaseMaintenanceStartup>();

        // Act
        bool success = await startup.RunAsync(token);

        // Assert
        Assert.True(success);
        Assert.True(startup.IsReady);
        maintenance.VerifyAll();
        Assert.Single(maintenance.Invocations);
        await Assert.ThrowsAsync<InvalidOperationException>(() => startup.RunAsync(token));
        Assert.Single(maintenance.Invocations);
    }

    [Theory]
    [InlineData("unconfirmed")]
    [InlineData("directory")]
    [InlineData("zero-timeout")]
    [InlineData("infinite-timeout")]
    [InlineData("operation")]
    public async Task Startup_InvalidDeploymentConfiguration_BlocksReadinessBeforeMaintenance(string invalid)
    {
        // Arrange
        DatabaseMaintenanceSettings settings = ValidSettings();
        if (invalid == "unconfirmed")
        {
            settings.SingleInitializerConfirmed = false;
        }
        else if (invalid == "directory")
        {
            settings.ServerBackupDirectory = "";
        }
        else if (invalid == "zero-timeout")
        {
            settings.TimeoutSeconds = 0;
        }
        else if (invalid == "infinite-timeout")
        {
            settings.TimeoutSeconds = int.MaxValue;
        }
        else
        {
            settings.Operation = (DatabaseStartupOperation)999;
        }

        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        await using ServiceProvider root = CreateRoot(maintenance, settings);
        DatabaseMaintenanceStartup startup = root.GetRequiredService<DatabaseMaintenanceStartup>();

        // Act
        bool success = await startup.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(success);
        Assert.False(startup.IsReady);
        maintenance.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(MaintenanceError.DatabaseMissing)]
    [InlineData(MaintenanceError.PermissionDenied)]
    [InlineData(MaintenanceError.DeadlineExceeded)]
    [InlineData(MaintenanceError.CleanupUnconfirmed)]
    public async Task Startup_Failure_BlocksReadinessAndRetry_LogsOnlySafeCodes(MaintenanceError code)
    {
        // Arrange
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        maintenance.Setup(item => item.UpdateExistingAsync(It.IsAny<TimeSpan>(), TestContext.Current.CancellationToken))
            .ThrowsAsync(new MaintenanceException(code, null, new InvalidOperationException("password=secret /private/path")));
        Mock<ILogger<DatabaseMaintenanceStartup>> logger = new();
        await using ServiceProvider root = CreateRoot(maintenance, ValidSettings(), logger);
        DatabaseMaintenanceStartup startup = root.GetRequiredService<DatabaseMaintenanceStartup>();

        // Act
        bool success = await startup.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(success);
        Assert.False(startup.IsReady);
        Assert.Single(maintenance.Invocations);
        var entry = Assert.Single(logger.Invocations, item => item.Method.Name == "Log");
        Assert.Null(entry.Arguments[3]);
        string text = entry.Arguments[2].ToString()!;
        Assert.Contains(code.ToString(), text);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("/private", text);
        await Assert.ThrowsAsync<InvalidOperationException>(() => startup.RunAsync(TestContext.Current.CancellationToken));
        Assert.Single(maintenance.Invocations);
    }

    [Fact]
    public async Task Startup_WaitsForAsyncScopeCleanupBeforeReadiness()
    {
        // Arrange
        TaskCompletionSource cleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cleanupRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        maintenance.As<IAsyncDisposable>().Setup(item => item.DisposeAsync()).Returns(async () =>
        {
            cleanupStarted.SetResult();
            await cleanupRelease.Task;
        });
        maintenance.Setup(item => item.UpdateExistingAsync(It.IsAny<TimeSpan>(), TestContext.Current.CancellationToken))
            .ReturnsAsync(new DatabaseMaintenanceResult(MaintenanceOutcome.Unchanged, [], null));
        await using ServiceProvider root = CreateRoot(maintenance, ValidSettings());
        DatabaseMaintenanceStartup startup = root.GetRequiredService<DatabaseMaintenanceStartup>();

        // Act
        Task<bool> operation = startup.RunAsync(TestContext.Current.CancellationToken);
        await cleanupStarted.Task;

        // Assert
        Assert.False(operation.IsCompleted);
        Assert.False(startup.IsReady);

        // Act
        cleanupRelease.SetResult();
        bool success = await operation;

        // Assert
        Assert.True(success);
        Assert.True(startup.IsReady);
    }

    [Fact]
    public async Task Startup_CallerCancellation_WaitsForServiceCleanupAndNeverBecomesReady()
    {
        // Arrange
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        TaskCompletionSource operationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cleanupRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        maintenance.Setup(item => item.UpdateExistingAsync(It.IsAny<TimeSpan>(), cancellation.Token))
            .Returns(async (TimeSpan timeout, CancellationToken token) =>
            {
                operationStarted.SetResult();
                await Task.Delay(Timeout.Infinite, token);
                throw new InvalidOperationException("Отмена должна прервать подставной provider.");
            });
        maintenance.As<IAsyncDisposable>().Setup(item => item.DisposeAsync()).Returns(async () =>
        {
            cleanupStarted.SetResult();
            await cleanupRelease.Task;
        });
        await using ServiceProvider root = CreateRoot(maintenance, ValidSettings());
        DatabaseMaintenanceStartup startup = root.GetRequiredService<DatabaseMaintenanceStartup>();

        // Act
        Task<bool> operation = startup.RunAsync(cancellation.Token);
        await operationStarted.Task;
        cancellation.Cancel();
        await cleanupStarted.Task;

        // Assert
        Assert.False(operation.IsCompleted);
        Assert.False(startup.IsReady);

        // Act
        cleanupRelease.SetResult();
        bool success = await operation;

        // Assert
        Assert.False(success);
        Assert.False(startup.IsReady);
        maintenance.Verify(item => item.InitializeNewAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Startup_CleanupFailureAfterSuccessfulMaintenance_DoesNotAdmitHost()
    {
        // Arrange
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        maintenance.Setup(item => item.UpdateExistingAsync(It.IsAny<TimeSpan>(), TestContext.Current.CancellationToken))
            .ReturnsAsync(new DatabaseMaintenanceResult(MaintenanceOutcome.Unchanged, [], null));
        maintenance.As<IAsyncDisposable>().Setup(item => item.DisposeAsync())
            .Throws(new InvalidOperationException("provider secret cleanup"));
        Mock<ILogger<DatabaseMaintenanceStartup>> logger = new();
        await using ServiceProvider root = CreateRoot(maintenance, ValidSettings(), logger);
        DatabaseMaintenanceStartup startup = root.GetRequiredService<DatabaseMaintenanceStartup>();

        // Act
        bool success = await startup.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(success);
        Assert.False(startup.IsReady);
        var entry = Assert.Single(logger.Invocations, item => item.Method.Name == "Log");
        Assert.Contains(MaintenanceError.CleanupUnconfirmed.ToString(), entry.Arguments[2].ToString());
        Assert.Null(entry.Arguments[3]);
        Assert.DoesNotContain("secret", entry.Arguments[2].ToString());
    }

    [Theory]
    [InlineData(MaintenanceError.DeadlineExceeded)]
    [InlineData(MaintenanceError.PermissionDenied)]
    public async Task Startup_DisposalFailureAfterOperationFailure_PreservesSafePrimaryCode(MaintenanceError primary)
    {
        // Arrange
        Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance = new(MockBehavior.Strict);
        maintenance.Setup(item => item.UpdateExistingAsync(It.IsAny<TimeSpan>(), TestContext.Current.CancellationToken))
            .ThrowsAsync(new MaintenanceException(primary, null, new InvalidOperationException("operation secret")));
        maintenance.As<IAsyncDisposable>().Setup(item => item.DisposeAsync())
            .Throws(new InvalidOperationException("cleanup secret"));
        Mock<ILogger<DatabaseMaintenanceStartup>> logger = new();
        await using ServiceProvider root = CreateRoot(maintenance, ValidSettings(), logger);
        DatabaseMaintenanceStartup startup = root.GetRequiredService<DatabaseMaintenanceStartup>();

        // Act
        bool success = await startup.RunAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(success);
        Assert.False(startup.IsReady);
        var entry = Assert.Single(logger.Invocations, item => item.Method.Name == "Log");
        Assert.Contains(MaintenanceError.CleanupUnconfirmed.ToString(), entry.Arguments[2].ToString());
        Assert.Contains(primary.ToString(), entry.Arguments[2].ToString());
        Assert.Null(entry.Arguments[3]);
        Assert.DoesNotContain("secret", entry.Arguments[2].ToString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => startup.RunAsync(TestContext.Current.CancellationToken));
    }

    private static DatabaseMaintenanceSettings ValidSettings()
    {
        return new DatabaseMaintenanceSettings
        {
            SingleInitializerConfirmed = true,
            ServerBackupDirectory = "/test/server-only/backups",
            TimeoutSeconds = 9
        };
    }

    private static ServiceProvider CreateRoot(Mock<IDatabaseMaintenance<CostVisionContextKey>> maintenance,
        DatabaseMaintenanceSettings settings, Mock<ILogger<DatabaseMaintenanceStartup>>? logger = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IOptions<DatabaseMaintenanceSettings>>(Options.Create(settings));
        services.AddScoped(_ => maintenance.Object);
        services.AddSingleton<DatabaseMaintenanceStartup>();
        if (logger != null)
        {
            services.AddSingleton(logger.Object);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
