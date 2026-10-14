using System.Data.Common;
using CostVision.Infrastructure.DataBase;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class Stage08aMaintenanceTests
{
    private static readonly TimeSpan TEST_TIMEOUT = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ActualCoordinator_NoPending_ReturnsUnchangedWithoutBackupOrMigration()
    {
        // Arrange
        Fixture data = new() { Pending = [] };

        // Act
        DatabaseMaintenanceResult result = await data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MaintenanceOutcome.Unchanged, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Empty(result.AppliedMigrations);
        Assert.Equal(new[] { "inspect", "pin", "inspect", "pending", "cleanup" }, data.Events);
        Assert.False(data.PinActive);
    }

    [Fact]
    public async Task ActualCoordinator_Pending_BackupAndTargetConfirmationPrecedeMigrationAndCleanup()
    {
        // Arrange
        Fixture data = new();

        // Act
        DatabaseMaintenanceResult result = await data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MaintenanceOutcome.Migrated, result.Outcome);
        Assert.Equal(new[] { "AddReceiptRowVersion" }, result.AppliedMigrations);
        Assert.NotNull(result.Backup);
        Assert.True(result.Backup.Artifact.IsConfirmed);
        Assert.Equal(new[] { "inspect", "pin", "inspect", "pending", "backup", "inspect", "migrate", "pending", "cleanup" }, data.Events);
        Assert.False(data.PinActive);
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("target")]
    [InlineData("unverified")]
    [InlineData("scope")]
    [InlineData("format")]
    [InlineData("time")]
    [InlineData("local")]
    public async Task ActualCoordinator_InvalidReceipt_BlocksMigration(string invalidField)
    {
        // Arrange
        Fixture data = new() { InvalidReceipt = invalidField };

        // Act
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MaintenanceError.BackupNotConfirmed, error.Code);
        Assert.DoesNotContain("migrate", data.Events);
        Assert.Equal("cleanup", data.Events.Last());
        Assert.False(data.PinActive);
    }

    [Fact]
    public async Task ActualCoordinator_TargetChangedAfterBackup_BlocksMigration()
    {
        // Arrange
        Fixture data = new() { ChangeTargetAfterBackup = true };

        // Act
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MaintenanceError.TargetMismatch, error.Code);
        Assert.Contains("backup", data.Events);
        Assert.DoesNotContain("migrate", data.Events);
        Assert.Equal("cleanup", data.Events.Last());
    }

    [Theory]
    [InlineData(MaintenanceError.AuthenticationFailed)]
    [InlineData(MaintenanceError.PermissionDenied)]
    [InlineData(MaintenanceError.ConnectionFailed)]
    public async Task ActualCoordinator_InspectionFailure_DoesNotMeanMissingOrInitialize(MaintenanceError code)
    {
        // Arrange
        Fixture data = new() { InspectionError = new MaintenanceException(code) };

        // Act
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(code, error.Code);
        Assert.Equal(new[] { "inspect" }, data.Events);
    }

    [Fact]
    public async Task ActualCoordinator_MissingDatabase_RequiresExplicitInitialization()
    {
        // Arrange
        Fixture data = new() { Exists = false };

        // Act
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MaintenanceError.DatabaseMissing, error.Code);
        Assert.DoesNotContain("create", data.Events);

        // Act
        DatabaseMaintenanceResult result = await data.Coordinator.InitializeNewAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MaintenanceOutcome.Initialized, result.Outcome);
        Assert.Null(result.Backup);
        Assert.Equal(1, data.Events.Count(item => item == "create"));
        Assert.DoesNotContain("backup", data.Events);
        Assert.Equal(1, data.Events.Count(item => item == "migrate"));
        Assert.Equal("cleanup", data.Events.Last());
    }

    [Fact]
    public async Task ActualCoordinator_InitializeExistingDatabase_IsRejectedBeforeWrites()
    {
        // Arrange
        Fixture data = new();

        // Act
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.InitializeNewAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MaintenanceError.DatabaseAlreadyExists, error.Code);
        Assert.Equal(new[] { "inspect" }, data.Events);
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("deadline")]
    [InlineData("cancel")]
    [InlineData("cleanup")]
    public async Task ActualCoordinator_UnknownMigrationOrCleanupOutcome_PoisonsGateWithoutRetry(string failure)
    {
        // Arrange
        Fixture data = new();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (failure == "failure")
        {
            data.MigrationAction = _ => throw new InvalidOperationException("provider password=secret /private/path");
        }
        else if (failure == "deadline")
        {
            data.MigrationAction = token => Task.Delay(Timeout.Infinite, token);
        }
        else if (failure == "cancel")
        {
            data.MigrationAction = token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            };
        }
        else
        {
            data.CleanupAction = () => throw new InvalidOperationException("cleanup secret");
        }

        TimeSpan timeout = TEST_TIMEOUT;
        if (failure == "deadline")
        {
            timeout = TimeSpan.FromMilliseconds(100);
        }

        // Act
        Exception? error = await Record.ExceptionAsync(() => data.Coordinator.UpdateExistingAsync(timeout, cancellation.Token));
        int eventsAtFailure = data.Events.Count;
        MaintenanceException poisoned = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));

        // Assert
        if (failure == "cancel")
        {
            Assert.IsAssignableFrom<OperationCanceledException>(error);
        }
        else if (failure == "deadline")
        {
            Assert.Equal(MaintenanceError.DeadlineExceeded, Assert.IsType<MaintenanceException>(error).Code);
        }
        else if (failure == "cleanup")
        {
            Assert.Equal(MaintenanceError.CleanupUnconfirmed, Assert.IsType<MaintenanceException>(error).Code);
        }
        else
        {
            Assert.Equal(MaintenanceError.MigrationFailed, Assert.IsType<MaintenanceException>(error).Code);
        }

        Assert.Equal(MaintenanceError.GatePoisoned, poisoned.Code);
        Assert.Equal(eventsAtFailure, data.Events.Count);
        Assert.Equal(1, data.Events.Count(item => item == "migrate"));
        Assert.Equal("cleanup", data.Events.Last());
        Assert.All(data.Logger.Exceptions, exception => Assert.Null(exception));
        Assert.DoesNotContain(data.Logger.Messages, message => message.Contains("secret") || message.Contains("/private"));
    }

    [Fact]
    public async Task ActualCoordinator_Cancellation_WaitsForCleanupBeforeReturningAndReleasingGate()
    {
        // Arrange
        Fixture data = new();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        TaskCompletionSource cleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource cleanupRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        data.MigrationAction = token =>
        {
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        data.CleanupAction = async () =>
        {
            cleanupStarted.SetResult();
            await cleanupRelease.Task;
        };

        // Act
        Task operation = data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, cancellation.Token);
        await cleanupStarted.Task;

        // Assert
        Assert.False(operation.IsCompleted);
        Assert.True(data.PinActive);

        // Act
        cleanupRelease.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        // Assert
        Assert.False(data.PinActive);
        Assert.Equal("cleanup", data.Events.Last());
        MaintenanceException poisoned = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.InspectAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));
        Assert.Equal(MaintenanceError.GatePoisoned, poisoned.Code);
    }

    [Theory]
    [InlineData("backup")]
    [InlineData("dispatch")]
    public async Task ActualCoordinator_BackupFailure_BlocksMigrationAndNeverRetries(string failure)
    {
        // Arrange
        Fixture data = new();
        MaintenanceError expected = MaintenanceError.BackupFailed;
        if (failure == "dispatch")
        {
            expected = MaintenanceError.CleanupUnconfirmed;
        }

        data.BackupError = new MaintenanceException(expected);

        // Act
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.UpdateExistingAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(expected, error.Code);
        Assert.Equal(1, data.Events.Count(item => item == "backup"));
        Assert.DoesNotContain("migrate", data.Events);
        Assert.Equal("cleanup", data.Events.Last());
        if (failure == "dispatch")
        {
            int calls = data.Events.Count;
            MaintenanceException poisoned = await Assert.ThrowsAsync<MaintenanceException>(() => data.Coordinator.InitializeNewAsync(TEST_TIMEOUT, TestContext.Current.CancellationToken));
            Assert.Equal(MaintenanceError.GatePoisoned, poisoned.Code);
            Assert.Equal(calls, data.Events.Count);
        }
    }

    private class Fixture : IDatabaseMaintenanceProvider<CostVisionContextKey>, IRelationalMigrationOperations<CostVisionContextKey>
    {
        public List<string> Events { get; } = [];
        public bool Exists { get; set; } = true;
        public bool PinActive { get; private set; }
        public IReadOnlyList<string> Pending { get; set; } = ["AddReceiptRowVersion"];
        public string? InvalidReceipt { get; set; }
        public bool ChangeTargetAfterBackup { get; set; }
        public MaintenanceException? InspectionError { get; set; }
        public MaintenanceException? BackupError { get; set; }
        public Func<CancellationToken, Task>? MigrationAction { get; set; }
        public Func<Task>? CleanupAction { get; set; }
        public RecordingLogger<DatabaseMaintenance<CostVisionContextKey>> Logger { get; } = new();
        public DatabaseMaintenance<CostVisionContextKey> Coordinator { get; }
        public DatabaseMaintenanceCapabilities Capabilities { get; } = new("sqlserver", "copy-only", "database", false, true);
        public string ProviderName => "Microsoft.EntityFrameworkCore.SqlServer";
        public DbConnection Connection => throw new InvalidOperationException("Подставной coordinator не открывает соединения.");
        public bool HasTransaction => false;

        public Fixture()
        {
            Coordinator = new DatabaseMaintenance<CostVisionContextKey>(this, this, new SingleInitializerGate(), Logger);
        }

        public Task<DatabaseInspection> InspectAsync(MaintenanceBudget budget)
        {
            budget.Check();
            Events.Add("inspect");
            if (InspectionError != null)
            {
                throw InspectionError;
            }

            string target = "target";
            if (ChangeTargetAfterBackup && Events.Contains("backup"))
            {
                target = "other-target";
            }

            return Task.FromResult(new DatabaseInspection(Exists, target, []));
        }

        public Task CreateAsync(MaintenanceBudget budget)
        {
            budget.Check();
            Events.Add("create");
            Exists = true;

            return Task.CompletedTask;
        }

        public Task<DatabaseBackupReceipt> BackupAsync(Guid operationId, string targetIdentity, MaintenanceBudget budget)
        {
            budget.Check();
            Assert.True(PinActive);
            Events.Add("backup");
            if (BackupError != null)
            {
                throw BackupError;
            }

            ServerBackupArtifact artifact = new("/test/server-only/backup.bak", Guid.NewGuid(), 1, true, true);
            DatabaseBackupReceipt receipt = new(operationId, targetIdentity, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                artifact, Capabilities.Provider, Capabilities.Format, Capabilities.Scope);
            if (InvalidReceipt == "operation")
            {
                receipt = receipt with { OperationId = Guid.NewGuid() };
            }
            else if (InvalidReceipt == "target")
            {
                receipt = receipt with { TargetIdentity = "other-target" };
            }
            else if (InvalidReceipt == "unverified")
            {
                receipt = receipt with { Artifact = artifact with { VerifyOnlySucceeded = false } };
            }
            else if (InvalidReceipt == "scope")
            {
                receipt = receipt with { Scope = "other-scope" };
            }
            else if (InvalidReceipt == "format")
            {
                receipt = receipt with { Format = "other-format" };
            }
            else if (InvalidReceipt == "time")
            {
                receipt = receipt with { CompletedAtUtc = DateTimeOffset.UtcNow.AddHours(1) };
            }
            else if (InvalidReceipt == "local")
            {
                receipt = receipt with { Artifact = new LocalBackupArtifact("/test/local", 1, new string('A', 64)) };
            }

            return Task.FromResult(receipt);
        }

        public Task<IAsyncDisposable> PinAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Events.Add("pin");
            PinActive = true;
            Mock<IAsyncDisposable> lease = new();
            lease.Setup(item => item.DisposeAsync()).Returns(async () =>
            {
                Events.Add("cleanup");
                if (CleanupAction != null)
                {
                    await CleanupAction();
                }

                PinActive = false;
            });

            return Task.FromResult(lease.Object);
        }

        public Task<IReadOnlyList<string>> PendingAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Assert.True(PinActive);
            Events.Add("pending");

            return Task.FromResult(Pending);
        }

        public async Task MigrateAsync(CancellationToken ct)
        {
            Assert.True(PinActive);
            Events.Add("migrate");
            if (MigrationAction != null)
            {
                await MigrationAction(ct);
            }

            ct.ThrowIfCancellationRequested();
            Pending = [];
        }
    }

    private class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel level)
        {
            return true;
        }

        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            Exceptions.Add(exception);
        }
    }
}
