using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Extensions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.Fixtures;

public class SqlServerContainerFixture : IAsyncLifetime
{
    private const string SA_PASSWORD = "Strong_password_123!";
    private const string MASTER_DATABASE_NAME = "master";
    private const string SERVER_CERTIFICATE_SETTING = "TrustServerCertificate=True";
    private const int SQL_STARTUP_RETRY_COUNT = 60;

    private readonly IContainer _container = new ContainerBuilder(DatabaseContainerSettings.ResolveImage())
        .WithEnvironment("ACCEPT_EULA", "Y")
        .WithEnvironment("MSSQL_SA_PASSWORD", SA_PASSWORD)
        .WithEnvironment("MSSQL_TELEMETRY_ENABLED", "0")
        .WithPortBinding(DatabaseContainerSettings.SQL_SERVER_PORT, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilExternalTcpPortIsAvailable(DatabaseContainerSettings.SQL_SERVER_PORT))
        .Build();

    public string ConnectionString => BuildConnectionString(DatabaseContainerSettings.DatabaseName);

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await WaitForSqlServerAsync();

        await using ServiceProvider serviceProvider = CreateServiceProvider();
        ApplicationContext context = serviceProvider.GetRequiredService<ApplicationContext>();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public ServiceProvider CreateServiceProvider()
    {
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:MSSql"] = ConnectionString,
            ["ApiEndpoints:ProverkachekaApiUrl"] = "https://integration-tests.invalid",
            ["ProverkachekaApiToken"] = "integration-tests"
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        ServiceCollection services = new();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private async Task WaitForSqlServerAsync()
    {
        string connectionString = BuildConnectionString(MASTER_DATABASE_NAME);
        Exception? lastException = null;

        foreach (int _ in Enumerable.Range(0, SQL_STARTUP_RETRY_COUNT))
        {
            try
            {
                await using SqlConnection connection = new(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new InvalidOperationException("SQL Server container did not become ready in time.", lastException);
    }

    private string BuildConnectionString(string databaseName)
    {
        string host = _container.Hostname;
        ushort port = _container.GetMappedPublicPort(DatabaseContainerSettings.SQL_SERVER_PORT);

        return $"Server={host},{port};Database={databaseName};User Id=sa;Password={SA_PASSWORD};{SERVER_CERTIFICATE_SETTING}";
    }
}
