using System.Data;
using System.Data.Common;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Extensions;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Extensions;
using CostVision.Infrastructure.Services.DataBase;
using EFCoreLibrary.Abstractions.Database;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using EFCoreLibrary.Maintenance.Abstractions;
using EFCoreLibrary.Maintenance.Coordination;
using EFCoreLibrary.Maintenance.Errors;
using EFCoreLibrary.Maintenance.Models;
using EFCoreLibrary.Maintenance.SqlServer.Options;
using EFCoreLibrary.Maintenance.SqlServer.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class ContextRepositoryAndMaintenanceLibraryTests
{
    [Fact]
    public async Task ActualDllRegistrations_UseOneMarkerContextTrackerAndMaintenance_WithoutIo()
    {
        // Arrange
        IConfiguration configuration = CreateConfiguration();
        ServiceCollection services = new();
        Mock<IDatabaseCommands> commands = new(MockBehavior.Strict);
        services.AddLogging();
        services.AddApplication();

        // Act
        services.AddInfrastructure(configuration);
        services.AddSingleton(commands.Object);
        await using ServiceProvider root = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        await using AsyncServiceScope first = root.CreateAsyncScope();
        await using AsyncServiceScope second = root.CreateAsyncScope();
        ApplicationContext context = first.ServiceProvider.GetRequiredService<ApplicationContext>();
        IAppDbContext<CostVisionContextKey> adapter = first.ServiceProvider.GetRequiredService<IAppDbContext<CostVisionContextKey>>();
        IUnitOfWorkContext<CostVisionContextKey> unit = first.ServiceProvider.GetRequiredService<IUnitOfWorkContext<CostVisionContextKey>>();
        IRelationalMigrationOperations<CostVisionContextKey> migration = first.ServiceProvider.GetRequiredService<IRelationalMigrationOperations<CostVisionContextKey>>();
        IDatabaseMaintenance<CostVisionContextKey> maintenance = first.ServiceProvider.GetRequiredService<IDatabaseMaintenance<CostVisionContextKey>>();
        IDatabaseMaintenanceProvider<CostVisionContextKey> maintenanceProvider = first.ServiceProvider.GetRequiredService<IDatabaseMaintenanceProvider<CostVisionContextKey>>();
        IContextCreateItemRepository<Product, CostVisionContextKey> create = first.ServiceProvider.GetRequiredService<IContextCreateItemRepository<Product, CostVisionContextKey>>();
        IContextDeleteItemRepository<Product, CostVisionContextKey> delete = first.ServiceProvider.GetRequiredService<IContextDeleteItemRepository<Product, CostVisionContextKey>>();
        IContextQueryRepository<Product, CostVisionContextKey> query = first.ServiceProvider.GetRequiredService<IContextQueryRepository<Product, CostVisionContextKey>>();
        Assert.True(Product.TryCreate("Product", "PRODUCT", out Product? product, out string? error), error);
        create.Create(product!);

        // Assert
        Assert.Same(context.Database, adapter.Database);
        Assert.Same(context.Database, unit.Database);
        Assert.Same(context.Database.GetDbConnection(), migration.Connection);
        Assert.Same(context.Entry(product!).Entity, unit.Entry(product!).Entity);
        Assert.Equal(context.Entry(product!).State, unit.Entry(product!).State);
        Assert.Equal(EntityState.Added, Assert.Single(context.ChangeTracker.Entries<Product>()).State);
        Assert.Contains("[Products]", query.Query(true).Where(item => item.NormalizedName == "PRODUCT").ToQueryString());
        Assert.IsType<DatabaseMaintenance<CostVisionContextKey>>(maintenance);
        Assert.IsType<SqlServerMaintenanceProvider<CostVisionContextKey>>(maintenanceProvider);
        Assert.Equal("sqlserver", maintenance.Capabilities.Provider);
        Assert.True(maintenance.Capabilities.ServerArtifact);
        Assert.False(maintenance.Capabilities.UsesExternalProcess);
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<ApplicationContext>());
        Assert.Same(root.GetRequiredService<SingleInitializerGate>(), first.ServiceProvider.GetRequiredService<SingleInitializerGate>());
        Assert.Same(root.GetRequiredService<SingleInitializerGate>(), second.ServiceProvider.GetRequiredService<SingleInitializerGate>());
        Assert.False(root.GetRequiredService<DatabaseMaintenanceStartup>().IsReady);
        Assert.Equal(ConnectionState.Closed, migration.Connection.State);
        commands.VerifyNoOtherCalls();
        Assert.Single(services, item => item.ServiceType == typeof(IAppDbContext<CostVisionContextKey>));

        // Act
        delete.Delete(product!);

        // Assert
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty(second.ServiceProvider.GetRequiredService<ApplicationContext>().ChangeTracker.Entries());
    }

    [Fact]
    public void ActualContext_PreservesMigrationsAssemblyAndModelIncludingRowVersion()
    {
        // Arrange
        using ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;ConnectRetryCount=0").Options);

        // Act
        IMigrationsAssembly history = context.GetService<IMigrationsAssembly>();
        bool changed = context.Database.HasPendingModelChanges();

        // Assert
        Assert.Equal(typeof(ApplicationContext).Assembly, history.Assembly);
        Assert.Contains(history.Migrations.Keys, name => name.EndsWith("AddReceiptRowVersion", StringComparison.Ordinal));
        Assert.False(changed);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData("ConnectRetryCount=1")]
    [InlineData("ConnectRetryCount=0;MultiSubnetFailover=true")]
    [InlineData("ConnectRetryCount=0;Failover Partner=other")]
    [InlineData("ConnectRetryCount=0;ApplicationIntent=ReadOnly")]
    public void ActualSqlServerModule_RejectsUnsupportedReconnectOrRoutingBeforeIo(string connectionOptions)
    {
        // Arrange
        using ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;" + connectionOptions).Options);
        Mock<IRelationalMigrationOperations<CostVisionContextKey>> migrations = CreateMigrations(context.Database.GetDbConnection());
        Mock<IDatabaseCommands> commands = new(MockBehavior.Strict);

        // Act
        MaintenanceException error = Assert.Throws<MaintenanceException>(() => new SqlServerMaintenanceProvider<CostVisionContextKey>(
            migrations.Object, commands.Object, new SqlServerMaintenanceOptions("/test/server-only/backups")));

        // Assert
        Assert.Equal(MaintenanceError.UnsupportedCapability, error.Code);
        commands.VerifyNoOtherCalls();
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSqlServerBackup_RequiresHeaderAndVerifyOnly_DispatchFailureIsUncertain(bool failVerify)
    {
        // Arrange
        using ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;ConnectRetryCount=0").Options);
        Mock<IRelationalMigrationOperations<CostVisionContextKey>> migrations = CreateMigrations(context.Database.GetDbConnection());
        Mock<IDatabaseCommands> commands = new(MockBehavior.Strict);
        List<string> events = [];
        Guid operation = Guid.NewGuid();
        string? backupPath = null;
        commands.Setup(item => item.ExecuteAsync(It.IsAny<DbConnection>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<MaintenanceBudget>(), It.IsAny<Action?>()))
            .Returns((DbConnection connection, string sql, IReadOnlyDictionary<string, object?> parameters, MaintenanceBudget budget, Action? dispatch) =>
            {
                Assert.Equal(ConnectionState.Closed, connection.State);
                Assert.NotNull(dispatch);
                dispatch();
                if (sql.StartsWith("BACKUP", StringComparison.Ordinal))
                {
                    events.Add("backup");
                    Assert.Contains("COPY_ONLY, CHECKSUM", sql);
                    Assert.Contains("NOFORMAT, NOINIT", sql);
                    backupPath = Assert.IsType<string>(parameters["@path"]);
                }
                else
                {
                    events.Add("verify");
                    Assert.Contains("RESTORE VERIFYONLY", sql);
                    Assert.Contains("CHECKSUM", sql);
                    Assert.Equal(3, parameters["@position"]);
                    Assert.Equal(backupPath, parameters["@path"]);
                    if (failVerify)
                    {
                        throw new OperationCanceledException("provider secret");
                    }
                }

                return Task.CompletedTask;
            });
        commands.Setup(item => item.QueryAsync(It.IsAny<DbConnection>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<MaintenanceBudget>(), It.IsAny<Action?>()))
            .Returns((DbConnection connection, string sql, IReadOnlyDictionary<string, object?> parameters, MaintenanceBudget budget, Action? dispatch) =>
            {
                events.Add("header");
                Assert.Contains("RESTORE HEADERONLY", sql);
                Assert.Equal(backupPath, parameters["@path"]);
                dispatch!();
                IReadOnlyDictionary<string, object?> header = new Dictionary<string, object?>
                {
                    ["DatabaseName"] = "unused", ["BackupName"] = operation.ToString("N"), ["BackupType"] = 1,
                    ["IsCopyOnly"] = true, ["HasBackupChecksums"] = true, ["BackupSetGUID"] = Guid.NewGuid().ToString(),
                    ["BackupFinishDate"] = DateTime.UtcNow, ["Position"] = 3
                };

                return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>([header]);
            });
        SqlServerMaintenanceProvider<CostVisionContextKey> provider = new(migrations.Object, commands.Object,
            new SqlServerMaintenanceOptions("/test/server-only/backups"));
        using MaintenanceBudget budget = new(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Act
        DatabaseBackupReceipt? receipt = null;
        Exception? error = await Record.ExceptionAsync(async () => receipt = await provider.BackupAsync(operation, "target", budget));

        // Assert
        Assert.Equal(new[] { "backup", "header", "verify" }, events);
        Assert.Equal("/test/server-only/backups/" + operation.ToString("N") + ".bak", backupPath);
        if (failVerify)
        {
            Assert.Equal(MaintenanceError.CleanupUnconfirmed, Assert.IsType<MaintenanceException>(error).Code);
            Assert.Null(receipt);
        }
        else
        {
            Assert.Null(error);
            ServerBackupArtifact artifact = Assert.IsType<ServerBackupArtifact>(receipt!.Artifact);
            Assert.True(artifact.IsConfirmed);
            Assert.Equal(3, artifact.Position);
            Assert.True(artifact.VerifyOnlySucceeded);
        }

        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(18456, MaintenanceError.AuthenticationFailed)]
    [InlineData(229, MaintenanceError.PermissionDenied)]
    [InlineData(-2, MaintenanceError.ConnectionFailed)]
    public async Task ActualSqlServerInspection_ConnectionOrPermissionErrorsNeverBecomeMissing(int number, MaintenanceError expected)
    {
        // Arrange
        using ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;ConnectRetryCount=0").Options);
        Mock<IRelationalMigrationOperations<CostVisionContextKey>> migrations = CreateMigrations(context.Database.GetDbConnection());
        Mock<IDatabaseCommands> commands = new(MockBehavior.Strict);
        commands.Setup(item => item.QueryAsync(It.IsAny<DbConnection>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<MaintenanceBudget>(), It.IsAny<Action?>()))
            .ThrowsAsync(UnitOfWorkTransactionFailureTests.CreateSqlException(number, "provider secret"));
        SqlServerMaintenanceProvider<CostVisionContextKey> provider = new(migrations.Object, commands.Object,
            new SqlServerMaintenanceOptions("/test/server-only/backups"));
        using MaintenanceBudget budget = new(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Act
        MaintenanceException error = await Assert.ThrowsAsync<MaintenanceException>(() => provider.InspectAsync(budget));

        // Assert
        Assert.Equal(expected, error.Code);
        Assert.Equal(expected.ToString(), error.Message);
        Assert.NotNull(error.InnerException);
        Assert.Single(commands.Invocations);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task ActualSqlServerInspection_4060RequiresFullVisibilityAndConfirmedAbsence(bool visible, bool exists)
    {
        // Arrange
        using ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;ConnectRetryCount=0").Options);
        Mock<IRelationalMigrationOperations<CostVisionContextKey>> migrations = CreateMigrations(context.Database.GetDbConnection());
        Mock<IDatabaseCommands> commands = new(MockBehavior.Strict);
        List<string> events = [];
        commands.Setup(item => item.QueryAsync(It.IsAny<DbConnection>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, object?>>(), It.IsAny<MaintenanceBudget>(), It.IsAny<Action?>()))
            .Returns((DbConnection connection, string sql, IReadOnlyDictionary<string, object?> parameters, MaintenanceBudget budget, Action? dispatch) =>
            {
                Assert.Equal(ConnectionState.Closed, connection.State);
                if (sql.Contains("database_recovery_status", StringComparison.Ordinal))
                {
                    events.Add("connect");
                    throw UnitOfWorkTransactionFailureTests.CreateSqlException(4060, "provider secret");
                }

                if (sql.Contains("VIEW ANY DATABASE", StringComparison.Ordinal))
                {
                    events.Add("visibility");
                    int visibility = 0;
                    if (visible)
                    {
                        visibility = 1;
                    }

                    return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                        [new Dictionary<string, object?> { ["visible"] = visibility, ["edition"] = 3 }]);
                }

                events.Add("absence");
                Assert.Contains("sys.databases", sql);
                Assert.Equal("unused", parameters["@database"]);
                if (exists)
                {
                    return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                        [new Dictionary<string, object?> { ["name"] = "unused" }]);
                }

                return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>([]);
            });
        SqlServerMaintenanceProvider<CostVisionContextKey> provider = new(migrations.Object, commands.Object,
            new SqlServerMaintenanceOptions("/test/server-only/backups"));
        using MaintenanceBudget budget = new(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Act
        DatabaseInspection? inspection = null;
        Exception? error = await Record.ExceptionAsync(async () => inspection = await provider.InspectAsync(budget));

        // Assert
        if (visible && !exists)
        {
            Assert.Null(error);
            Assert.NotNull(inspection);
            Assert.False(inspection.Exists);
            Assert.NotEmpty(inspection.TargetIdentity);
        }
        else
        {
            Assert.Equal(MaintenanceError.PermissionDenied, Assert.IsType<MaintenanceException>(error).Code);
            Assert.Null(inspection);
        }

        Assert.Equal(1, events.Count(item => item == "connect"));
        Assert.Contains("visibility", events);
        if (!visible)
        {
            Assert.DoesNotContain("absence", events);
        }

        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void EfLogging_DoesNotForwardProviderExceptionCategoryScopeSqlOrFormatter()
    {
        // Arrange
        Mock<ILogger<SafeDatabaseLoggerFactory>> target = new();
        SafeDatabaseLoggerFactory factory = new(target.Object);
        ILogger logger = factory.CreateLogger("provider-secret-category");
        bool formatterCalled = false;

        // Act
        using IDisposable? scope = logger.BeginScope("password=secret");
        logger.Log(LogLevel.Error, new EventId(20102, "secret-event"), "SQL secret",
            new InvalidOperationException("provider secret", new IOException("/private/path")), (state, exception) =>
            {
                formatterCalled = true;
                return state;
            });

        // Assert
        Assert.False(formatterCalled);
        var entry = Assert.Single(target.Invocations, item => item.Method.Name == "Log");
        Assert.Null(entry.Arguments[3]);
        string text = Assert.IsType<string>(entry.Arguments[2]?.ToString());
        Assert.Equal("EF database event: 20102", text);
        Assert.Null(scope);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MSSql"] = "Server=unused;Database=unused;ConnectRetryCount=0",
            ["ApiEndpoints:ProverkachekaApiUrl"] = "https://unused.invalid",
            ["ProverkachekaApiToken"] = "unused",
            ["DatabaseMaintenance:SingleInitializerConfirmed"] = "true",
            ["DatabaseMaintenance:ServerBackupDirectory"] = "/test/server-only/backups"
        }).Build();
    }

    private static Mock<IRelationalMigrationOperations<CostVisionContextKey>> CreateMigrations(DbConnection connection)
    {
        Mock<IRelationalMigrationOperations<CostVisionContextKey>> migrations = new(MockBehavior.Strict);
        migrations.SetupGet(item => item.Connection).Returns(connection);
        migrations.SetupGet(item => item.ProviderName).Returns("Microsoft.EntityFrameworkCore.SqlServer");
        migrations.SetupGet(item => item.HasTransaction).Returns(false);

        return migrations;
    }
}
