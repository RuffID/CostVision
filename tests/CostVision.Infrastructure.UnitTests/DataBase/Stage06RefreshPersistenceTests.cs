using CostVision.Tests;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.DataBase.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using CostVision.Application.Extensions;
using CostVision.Infrastructure.Extensions;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Moq.Protected;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class Stage06RefreshPersistenceTests
{
    [Fact]
    public async Task ActualDllBatchQuery_UsesOrderedTopAgainstFakeConnectionOnly()
    {
        // Arrange
        List<string> queries = [];
        List<object?> parameterValues = [];
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
                queries.Add(command.Object.CommandText);
                parameterValues.AddRange(parametersOwner.Parameters.Cast<SqlParameter>().Select(item => item.Value));
                return Task.FromResult<DbDataReader>(new DataTable().CreateDataReader());
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

        // Act
        await scope.ServiceProvider.GetRequiredService<IRefreshPendingReceiptsUseCase>()
            .ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        string sql = Assert.Single(queries);
        Assert.Contains("SELECT TOP(", sql);
        Assert.Contains("FROM [Receipts]", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("[CreatedAtUtc]", sql);
        Assert.Contains("[Id]", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("[RefreshAttemptCount]", sql);
        Assert.DoesNotContain("FROM (", sql);
        Assert.Contains(32, parameterValues);
        connection.Verify(item => item.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ActualRegistrations_ResolveIndependentRefreshContextsWithoutStartingHosting()
    {
        // Arrange
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
        await using ServiceProvider provider = registrations.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        // Act
        await using AsyncServiceScope first = provider.CreateAsyncScope();
        await using AsyncServiceScope second = provider.CreateAsyncScope();
        IReceiptRefreshUnitOfWork firstWork = first.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();
        IReceiptRefreshUnitOfWork secondWork = second.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();
        ApplicationContext firstContext = first.ServiceProvider.GetRequiredService<ApplicationContext>();
        ApplicationContext secondContext = second.ServiceProvider.GetRequiredService<ApplicationContext>();
        IReceiptRefreshWorkflow firstWorkflow = first.ServiceProvider.GetRequiredService<IReceiptRefreshWorkflow>();
        IReceiptRefreshWorkflow secondWorkflow = second.ServiceProvider.GetRequiredService<IReceiptRefreshWorkflow>();
        IRefreshPendingReceiptsUseCase pending = first.ServiceProvider.GetRequiredService<IRefreshPendingReceiptsUseCase>();

        // Assert
        Assert.NotSame(firstWork, secondWork);
        Assert.NotSame(firstContext, secondContext);
        Assert.NotSame(firstWorkflow, secondWorkflow);
        Assert.NotNull(pending);
        Assert.Same(firstWork, first.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>());
        Assert.Equal(System.Data.ConnectionState.Closed, firstContext.Database.GetDbConnection().State);
        Assert.Equal(System.Data.ConnectionState.Closed, secondContext.Database.GetDbConnection().State);
    }

    [Fact]
    public void ReceiptVersion_IsDatabaseGeneratedConcurrencyToken()
    {
        // Arrange
        using ApplicationContext context = CreateContext();

        // Act
        IProperty property = context.Model.FindEntityType(typeof(Receipt))!.FindProperty(nameof(Receipt.RowVersion))!;

        // Assert
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
        Assert.Equal("rowversion", property.GetColumnType());
        Assert.False(property.IsNullable);
        Assert.True(typeof(Receipt).GetProperty(nameof(Receipt.RowVersion))!.SetMethod!.IsPrivate);
        IProperty revision = context.Model.FindEntityType(typeof(Receipt))!.FindProperty(nameof(Receipt.RefreshRevision))!;
        Assert.Equal("bigint", revision.GetColumnType());
        Assert.False(revision.IsNullable);
        Assert.Equal(0L, revision.GetDefaultValue());
        Assert.True(typeof(Receipt).GetProperty(nameof(Receipt.RefreshRevision))!.SetMethod!.IsPrivate);
    }

    [Fact]
    public async Task ActualBatchPredicate_TranslatesOrderingBoundAndGuidCursorWithoutConnection()
    {
        // Arrange
        using ApplicationContext context = CreateContext();
        List<string> queries = [];
        Assert.True(Receipt.TryCreate("fn", "fd", "fp", new DateTime(2026, 1, 1), ReceiptOperationType.Income,
            100m, Guid.NewGuid(), new DateTime(2026, 1, 1), out Receipt? receipt, out _));
        receipt!.Id = Guid.NewGuid();
        Mock<IReceiptRepository> repository = new(MockBehavior.Strict);
        repository.Setup(item => item.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(), 0,
                RefreshPendingReceiptsUseCase.BATCH_SIZE, true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool noTracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
            {
                queries.Add(include(context.Set<Receipt>().AsNoTracking().Where(predicate)).Take(take!.Value).ToQueryString());
                return include(new[] { receipt }.AsQueryable().Where(predicate)).Take(take.Value).ToList();
            });
        Mock<ITestStorage> work = new(MockBehavior.Strict);
        work.SetupGet(item => item.Receipt).Returns(repository.Object);
        Mock<IReceiptRefreshWorkflow> workflow = new(MockBehavior.Strict);
        workflow.Setup(item => item.RefreshScheduledAsync(It.IsAny<Receipt>(), It.IsAny<DateTime>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "not available"));
        Mock<IServiceProvider> provider = new(MockBehavior.Strict);
        provider.Setup(item => item.GetService(typeof(IReceiptRepository))).Returns(repository.Object);
        provider.Setup(item => item.GetService(typeof(IReceiptRefreshWorkflow))).Returns(workflow.Object);
        Mock<IServiceScopeFactory> factory = new(MockBehavior.Strict);
        factory.Setup(item => item.CreateScope()).Returns(() =>
        {
            Mock<IServiceScope> scope = new(MockBehavior.Strict);
            scope.SetupGet(item => item.ServiceProvider).Returns(provider.Object);
            scope.Setup(item => item.Dispose());
            return scope.Object;
        });
        RefreshPendingReceiptsUseCase useCase = new(factory.Object,
            Mock.Of<ILogger<RefreshPendingReceiptsUseCase>>(), TimeProvider.System);

        // Act
        await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, queries.Count);
        Assert.All(queries, sql =>
        {
            Assert.Contains("TOP(", sql);
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("[CreatedAtUtc]", sql);
            Assert.Contains("[Id]", sql);
            Assert.Contains("NOT EXISTS", sql);
            Assert.Contains("[RefreshAttemptCount]", sql);
            Assert.Contains("[NextRefreshAttemptAtUtc]", sql);
            Assert.Contains("[RowVersion]", sql);
        });
        Assert.Contains("cursorId", queries.Last());
        Assert.Contains(">", queries.Last());
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData(2601, "Cannot insert duplicate key row with unique index 'IX_Products_NormalizedName'.", true)]
    [InlineData(2627, "Violation of UNIQUE KEY constraint 'IX_Stores_NormalizedName_NormalizedAddress'.", true)]
    [InlineData(2601, "Cannot insert duplicate key row with unique index 'IX_Products_AdaptiveName'.", false)]
    [InlineData(547, "Violation of UNIQUE KEY constraint 'IX_Products_NormalizedName'.", false)]
    [InlineData(2601, "The duplicate key value is (IX_Products_NormalizedName).", false)]
    public void CatalogConflict_RecognizesOnlyExpectedNormalizedIndexes(int number, string message, bool expected)
    {
        // Arrange
        string serverMessage = message;

        // Act
        bool result = ReceiptWriteErrors.IsCatalogUniqueViolation(number, serverMessage);

        // Assert
        Assert.Equal(expected, result);
    }

    private static ApplicationContext CreateContext()
    {
        return new ApplicationContext(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True").Options);
    }
}
