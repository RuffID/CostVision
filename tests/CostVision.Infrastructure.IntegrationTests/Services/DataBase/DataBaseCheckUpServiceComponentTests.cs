using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using CostVision.Infrastructure.IntegrationTests.Web.Helpers;
using CostVision.Infrastructure.Services.DataBase;
using EFCoreLibrary.EfCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.Services.DataBase;

public class DataBaseCheckUpServiceComponentTests(SqlServerContainerFixture fixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task CheckOrUpdateDB_AvailableDatabaseWithoutPendingMigrations_LogsNoChanges()
    {
        await using ApplicationContext context = CreateContext(fixture.ConnectionString);
        ListLoggerProvider loggerProvider = new();
        using ILoggerFactory loggerFactory = CreateLoggerFactory(loggerProvider);
        FakeBackupService backupService = new();
        DataBaseCheckUpService<ApplicationContext> service = new(
            new EfDbContextAdapter<ApplicationContext>(context),
            loggerFactory,
            backupService);

        service.CheckOrUpdateDB();

        Assert.False(backupService.WasCalled);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("Connection to the database was successful."));
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("No changes to the database."));
    }

    [Fact]
    public void CheckOrUpdateDB_UnavailableDatabase_ThrowsAndLogsConnectionError()
    {
        string connectionString = "Server=localhost,1;Database=CostVisionUnavailable;User Id=sa;Password=bad;TrustServerCertificate=True;Connection Timeout=1";
        using ApplicationContext context = CreateContext(connectionString);
        ListLoggerProvider loggerProvider = new();
        using ILoggerFactory loggerFactory = CreateLoggerFactory(loggerProvider);
        DataBaseCheckUpService<ApplicationContext> service = new(
            new EfDbContextAdapter<ApplicationContext>(context),
            loggerFactory,
            new FakeBackupService());

        Assert.ThrowsAny<Exception>(service.CheckOrUpdateDB);
        Assert.Contains(loggerProvider.Entries, entry =>
            entry.LogLevel == LogLevel.Error &&
            entry.Message.Contains("Failed to connect to the database."));
    }

    [Fact]
    public async Task CheckOrUpdateDB_PendingMigrationsExist_CreatesBackupAndAppliesMigrations()
    {
        string databaseName = $"CostVisionPendingMigrations_{Guid.NewGuid():N}";
        string connectionString = BuildConnectionString(fixture.ConnectionString, databaseName);

        await CreateDatabaseAsync(fixture.ConnectionString, databaseName);

        try
        {
            await using ApplicationContext context = CreateContext(connectionString);
            ListLoggerProvider loggerProvider = new();
            using ILoggerFactory loggerFactory = CreateLoggerFactory(loggerProvider);
            FakeBackupService backupService = new();
            DataBaseCheckUpService<ApplicationContext> service = new(
                new EfDbContextAdapter<ApplicationContext>(context),
                loggerFactory,
                backupService);

            service.CheckOrUpdateDB();

            Assert.True(backupService.WasCalled);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
            Assert.Contains(loggerProvider.Entries, entry =>
                entry.LogLevel == LogLevel.Information &&
                entry.Message.Contains("Database was updated."));
        }
        finally
        {
            await using ApplicationContext cleanupContext = CreateContext(connectionString);
            await cleanupContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        }
    }

    private static ApplicationContext CreateContext(string connectionString)
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new ApplicationContext(options);
    }

    private static ILoggerFactory CreateLoggerFactory(ListLoggerProvider loggerProvider)
    {
        return LoggerFactory.Create(builder => builder.AddProvider(loggerProvider));
    }

    private static string BuildConnectionString(string baseConnectionString, string databaseName)
    {
        SqlConnectionStringBuilder builder = new(baseConnectionString)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string baseConnectionString, string databaseName)
    {
        SqlConnectionStringBuilder builder = new(baseConnectionString)
        {
            InitialCatalog = "master"
        };

        await using SqlConnection connection = new(builder.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FakeBackupService : IBackupService<ApplicationContext>
    {
        public bool WasCalled { get; private set; }

        public void CreateSqlServerBackup()
        {
            WasCalled = true;
        }
    }
}
