using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using System.Data;
using System.Data.Common;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Extensions;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class ProjectedReadRepositoryExecutionTests
{
    [Fact]
    public async Task ActualDllQuery_ExecutesProjectedCountAndOrderedPageWithoutRealConnection()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        List<string> sql = [];
        List<object?> values = [];
        CancellationToken token = TestContext.Current.CancellationToken;
        Mock<DbConnection> connection = new();
        connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
        connection.SetupGet(item => item.ConnectionString).Returns("Server=unused;Database=unused");
        connection.SetupGet(item => item.ServerVersion).Returns("16.0");
        Mock<DbCommand> command = new();
        command.SetupProperty(item => item.CommandText);
        using SqlCommand parametersOwner = new();
        command.Protected().Setup<DbParameterCollection>("DbParameterCollection").Returns(parametersOwner.Parameters);
        command.Protected().Setup<DbParameter>("CreateDbParameter").Returns(() => new SqlParameter());
        command.Protected().Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync",
                ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
            .Returns((CommandBehavior behavior, CancellationToken ct) =>
            {
                Assert.Equal(token, ct);
                sql.Add(command.Object.CommandText);
                values.AddRange(parametersOwner.Parameters.Cast<SqlParameter>().Select(parameter => parameter.Value));
                DataTable table = new();
                if (command.Object.CommandText.Contains("COUNT(*)", StringComparison.Ordinal))
                {
                    table.Columns.Add("Count", typeof(int));
                    table.Rows.Add(4);
                }
                else
                {
                    table.Columns.Add("Id", typeof(Guid));
                    table.Rows.Add(id);
                }
                return Task.FromResult<DbDataReader>(table.CreateDataReader());
            });
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MSSql"] = "Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True",
            ["ApiEndpoints:ProverkachekaApiUrl"] = "https://unused.invalid",
            ["ProverkachekaApiToken"] = "unused-test-value"
        }).Build();
        ServiceCollection registrations = new();
        registrations.AddLogging();
        registrations.AddApplication();
        registrations.AddInfrastructure(configuration);
        registrations.AddScoped(_ => new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer(connection.Object).Options));
        await using ServiceProvider provider = registrations.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IReceiptRepository receipts = scope.ServiceProvider.GetRequiredService<IReceiptRepository>();
        IProductRepository products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        IStoreRepository stores = scope.ServiceProvider.GetRequiredService<IStoreRepository>();
        DateTime date = new(2026, 1, 15);

        // Act
        List<int> count = await receipts.GetProjectedItemsAsync(receipt => receipt.DateTime >= date,
            query => query.GroupBy(receipt => 1).Select(group => group.Count()), token);
        List<Guid> page = await receipts.GetProjectedItemsAsync(receipt => receipt.DateTime >= date,
            query => query.OrderByDescending(receipt => receipt.DateTime).ThenBy(receipt => receipt.Id)
                .Skip(2).Take(1).Select(receipt => receipt.Id), token);
        int productCount = await products.CountByPredicateAsync(product => product.NormalizedName == "TEST-PRODUCT", token);
        int storeCount = await stores.CountByPredicateAsync(store => store.NormalizedName == "TEST-STORE", token);

        // Assert
        Assert.Equal(4, Assert.Single(count));
        Assert.Equal(id, Assert.Single(page));
        Assert.Equal(4, sql.Count);
        Assert.Equal(4, productCount);
        Assert.Equal(4, storeCount);
        Assert.Contains("COUNT(*)", sql[0]);
        Assert.Contains("WHERE", sql[0]);
        Assert.Contains("ORDER BY", sql[1]);
        Assert.Contains("OFFSET", sql[1]);
        Assert.Contains("FETCH NEXT", sql[1]);
        Assert.DoesNotContain("[RowVersion]", sql[1]);
        Assert.Contains("COUNT(*)", sql[2]);
        Assert.Contains("COUNT(*)", sql[3]);
        Assert.Contains("WHERE", sql[2]);
        Assert.Contains("WHERE", sql[3]);
        Assert.Contains("TEST-PRODUCT", sql[2]);
        Assert.Contains("TEST-STORE", sql[3]);
        Assert.Contains(date, values);
        Assert.Empty(scope.ServiceProvider.GetRequiredService<ApplicationContext>().ChangeTracker.Entries());
        connection.Verify(item => item.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
