using System.Data;
using System.Data.Common;
using System.Reflection;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Extensions;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class ScenarioUnitOfWorkScopeTests
{
    [Fact]
    public async Task ActualRegistrations_AllAdaptersAndDllRepositoriesShareTracker_AndScopesAreIndependent()
    {
        // Arrange
        ServiceCollection services = CreateRegistrations();
        await using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using AsyncServiceScope first = provider.CreateAsyncScope();
        await using AsyncServiceScope second = provider.CreateAsyncScope();
        ApplicationContext firstContext = first.ServiceProvider.GetRequiredService<ApplicationContext>();
        ApplicationContext secondContext = second.ServiceProvider.GetRequiredService<ApplicationContext>();
        Type[] contracts = typeof(IUnitOfWorkScope).Assembly.GetTypes()
            .Where(type => type.IsInterface && type != typeof(IUnitOfWorkScope) &&
                typeof(IUnitOfWorkScope).IsAssignableFrom(type)).ToArray();
        Receipt receipt = CreateReceipt();
        Assert.True(Product.TryCreate("Product", "PRODUCT", out Product? product, out string? productError), productError);
        Assert.True(Store.TryCreate("Store", "STORE", null, null, out Store? store, out string? storeError), storeError);

        // Act
        foreach (Type contract in contracts)
        {
            object firstWork = first.ServiceProvider.GetRequiredService(contract);
            object secondWork = second.ServiceProvider.GetRequiredService(contract);
            Assert.NotSame(firstWork, secondWork);
            Assert.Same(firstWork, first.ServiceProvider.GetRequiredService(contract));
            PropertyInfo[] repositories = contract.GetProperties();
            Assert.InRange(repositories.Length, 2, 4);
            foreach (PropertyInfo property in repositories)
            {
                Assert.Same(first.ServiceProvider.GetRequiredService(property.PropertyType), property.GetValue(firstWork));
                Assert.NotSame(property.GetValue(firstWork), property.GetValue(secondWork));
            }
        }

        first.ServiceProvider.GetRequiredService<IReceiptCreationUnitOfWork>().Receipt.Create(receipt);
        first.ServiceProvider.GetRequiredService<IProductNamingUnitOfWork>().Product.Create(product!);
        first.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>().Store.Create(store!);

        // Assert
        Assert.Equal(14, contracts.Length);
        Assert.Empty(typeof(IUnitOfWorkScope).GetProperties());
        Assert.Null(typeof(IUnitOfWorkScope).Assembly.GetType(
            "CostVision.Application.Abstractions.DataBase.Repositories.IUnitOfWork"));
        Assert.NotSame(firstContext, secondContext);
        Assert.Same(receipt, firstContext.ChangeTracker.Entries<Receipt>().Single().Entity);
        Assert.Same(product, firstContext.ChangeTracker.Entries<Product>().Single().Entity);
        Assert.Same(store, firstContext.ChangeTracker.Entries<Store>().Single().Entity);
        Assert.Empty(secondContext.ChangeTracker.Entries());
        Assert.Equal(ConnectionState.Closed, firstContext.Database.GetDbConnection().State);
        Assert.Equal(ConnectionState.Closed, secondContext.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task OrdinaryTransaction_UsesOneOwner_AutoSavesBeforeCommit_AndPassesToken()
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IReceiptCreationUnitOfWork writer = scope.ServiceProvider.GetRequiredService<IReceiptCreationUnitOfWork>();
        IReceiptRefreshUnitOfWork other = scope.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();
        RecordingContext context = (RecordingContext)scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        using CancellationTokenSource cancellation = new();
        Receipt receipt = CreateReceipt();

        // Act
        await writer.ExecuteInTransaction(ct =>
        {
            Assert.Equal(cancellation.Token, ct);
            data.Events.Add("action");
            other.Receipt.Create(receipt);
            return Task.CompletedTask;
        }, cancellation.Token);

        // Assert
        Assert.Equal(new[] { "begin", "action", "save", "commit" }, data.Events);
        Assert.Single(context.SaveTokens, cancellation.Token);
        Assert.Same(receipt, context.ChangeTracker.Entries<Receipt>().Single().Entity);
        data.Transaction.Verify(item => item.CommitAsync(cancellation.Token), Times.Once);
        data.Transaction.Verify(item => item.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        data.Connection.Protected().Verify("BeginDbTransactionAsync", Times.Once(),
            IsolationLevel.Unspecified, cancellation.Token);
    }

    [Fact]
    public async Task ExplicitTransaction_PreservesIntermediateId_AndSavesOnlyAtRequestedStepsAcrossAdapters()
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IReceiptLinkUnitOfWork creator = scope.ServiceProvider.GetRequiredService<IReceiptLinkUnitOfWork>();
        IReceiptReconciliationUnitOfWork linker = scope.ServiceProvider.GetRequiredService<IReceiptReconciliationUnitOfWork>();
        RecordingContext context = (RecordingContext)scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        Guid userId = Guid.NewGuid();
        Receipt receipt = CreateReceipt();
        Assert.True(MoneyMovement.TryCreateManual(Guid.NewGuid(), 100m, MoneyMovementType.Expense,
            new DateTime(2026, 1, 1), null, userId, userId, DateTime.UtcNow,
            out MoneyMovement? movement, out string? error), error);
        creator.Receipt.Create(receipt);
        creator.MoneyMovement.Create(movement!);
        Guid idBeforeSave = movement!.Id;
        using CancellationTokenSource cancellation = new();

        // Act
        await creator.ExecuteInTransaction(async ct =>
        {
            await creator.SaveChangesAsync(ct);
            data.Events.Add("id");
            Assert.NotEqual(Guid.Empty, movement.Id);
            Assert.NotEqual(idBeforeSave, movement.Id);
            Assert.True(MoneyMovementReceipt.TryCreate(movement.Id, receipt.Id, userId, DateTime.UtcNow,
                out MoneyMovementReceipt? link, out string? linkError), linkError);
            linker.MoneyMovementReceipt.Create(link!);
            await linker.SaveChangesAsync(ct);
        }, IsolationLevel.Serializable, cancellation.Token);

        // Assert
        Assert.Equal(new[] { "begin", "save", "id", "save", "commit" }, data.Events);
        Assert.Equal(new[] { cancellation.Token, cancellation.Token }, context.SaveTokens);
        MoneyMovementReceipt createdLink = context.ChangeTracker.Entries<MoneyMovementReceipt>().Single().Entity;
        Assert.Equal(movement.Id, createdLink.MoneyMovementId);
        Assert.Equal(receipt.Id, createdLink.ReceiptId);
        data.Connection.Protected().Verify("BeginDbTransactionAsync", Times.Once(),
            IsolationLevel.Serializable, cancellation.Token);
    }

    [Fact]
    public async Task ExplicitTransaction_WithNoChanges_DoesNotSave()
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IReceiptRefreshUnitOfWork writer = scope.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();

        // Act
        await writer.ExecuteInTransaction(_ => Task.CompletedTask, IsolationLevel.Serializable, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "begin", "commit" }, data.Events);
    }

    [Theory]
    [InlineData("begin", false)]
    [InlineData("begin", true)]
    [InlineData("action", false)]
    [InlineData("action", true)]
    [InlineData("save", false)]
    [InlineData("save", true)]
    [InlineData("commit", false)]
    [InlineData("commit", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    public async Task FailureThroughOneAdapter_PoisonsAllWritersAndBothOverloads_OnlyNewScopeCanRetry(
        string failureStage, bool explicitIsolation)
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IReceiptCreationUnitOfWork writer = scope.ServiceProvider.GetRequiredService<IReceiptCreationUnitOfWork>();
        IStoreNamingUnitOfWork other = scope.ServiceProvider.GetRequiredService<IStoreNamingUnitOfWork>();
        IUnitOfWorkScope shared = scope.ServiceProvider.GetRequiredService<IUnitOfWorkScope>();
        RecordingContext context = (RecordingContext)scope.ServiceProvider.GetRequiredService<ApplicationContext>();
        writer.Receipt.Create(CreateReceipt());
        InvalidOperationException failure = new("storage failure");
        data.FailureStage = failureStage;
        data.Failure = failure;
        using CancellationTokenSource cancellation = new();
        Func<CancellationToken, Task> action = async ct =>
        {
            if (failureStage == "action")
            {
                throw failure;
            }

            if (failureStage == "cancel")
            {
                cancellation.Cancel();
                ct.ThrowIfCancellationRequested();
            }

            if (explicitIsolation)
            {
                await writer.SaveChangesAsync(ct);
            }
        };

        // Act
        Exception? thrown;
        if (explicitIsolation)
        {
            thrown = await Record.ExceptionAsync(() =>
                writer.ExecuteInTransaction(action, IsolationLevel.Serializable, cancellation.Token));
        }
        else
        {
            thrown = await Record.ExceptionAsync(() => writer.ExecuteInTransaction(action, cancellation.Token));
        }

        int callsAtFailure = data.Events.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.SaveChangesAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => shared.SaveChangesAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            other.ExecuteInTransaction(_ => Task.CompletedTask, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            other.ExecuteInTransaction(_ => Task.CompletedTask, IsolationLevel.Serializable, CancellationToken.None));

        // Assert
        if (failureStage == "cancel")
        {
            Assert.IsAssignableFrom<OperationCanceledException>(thrown);
        }
        else
        {
            Assert.Same(failure, thrown);
        }

        Assert.Equal(callsAtFailure, data.Events.Count);
        Assert.NotEmpty(context.ChangeTracker.Entries());
        if (failureStage == "begin")
        {
            data.Transaction.Verify(item => item.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            data.Transaction.Verify(item => item.RollbackAsync(CancellationToken.None), Times.Once);
        }

        // Act
        data.FailureStage = null;
        await using AsyncServiceScope fresh = data.Provider.CreateAsyncScope();
        await fresh.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>().SaveChangesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(callsAtFailure + 1, data.Events.Count);
        Assert.Equal("save", data.Events.Last());
        Assert.Empty(fresh.ServiceProvider.GetRequiredService<ApplicationContext>().ChangeTracker.Entries());
    }

    [Fact]
    public async Task NestedTransactionThroughDifferentAdapter_FailsBeforeSecondBegin_AndCannotSaveAfterRollback()
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IReceiptCreationUnitOfWork outer = scope.ServiceProvider.GetRequiredService<IReceiptCreationUnitOfWork>();
        IReceiptRefreshUnitOfWork inner = scope.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => outer.ExecuteInTransaction(
            ct => inner.ExecuteInTransaction(_ => Task.CompletedTask, IsolationLevel.Serializable, ct),
            IsolationLevel.Serializable, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => inner.SaveChangesAsync(CancellationToken.None));

        // Assert
        Assert.Equal(new[] { "begin", "rollback" }, data.Events);
        data.Connection.Protected().Verify("BeginDbTransactionAsync", Times.Once(),
            IsolationLevel.Serializable, CancellationToken.None);
    }

    [Fact]
    public async Task UnknownSaveFailureOutsideTransaction_AlsoPoisonsSharedScope()
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IProductNamingUnitOfWork writer = scope.ServiceProvider.GetRequiredService<IProductNamingUnitOfWork>();
        IReceiptRefreshUnitOfWork other = scope.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();
        data.FailureStage = "save";
        data.Failure = new DbUpdateConcurrencyException("concurrent change");

        // Act
        Exception? thrown = await Record.ExceptionAsync(() => writer.SaveChangesAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.SaveChangesAsync(CancellationToken.None));

        // Assert
        Assert.Same(data.Failure, thrown);
        Assert.Equal(new[] { "save" }, data.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaughtSaveFailureInsideAction_CannotCommitThroughAnotherAdapter(bool explicitIsolation)
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IReceiptCreationUnitOfWork owner = scope.ServiceProvider.GetRequiredService<IReceiptCreationUnitOfWork>();
        IReceiptRefreshUnitOfWork writer = scope.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();
        data.FailureStage = "save";
        data.Failure = new DbUpdateException("save failed");
        Func<CancellationToken, Task> action = async ct =>
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => writer.SaveChangesAsync(ct));
        };

        // Act
        if (explicitIsolation)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                owner.ExecuteInTransaction(action, IsolationLevel.Serializable, CancellationToken.None));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ExecuteInTransaction(action, CancellationToken.None));
        }

        // Assert
        Assert.Equal(new[] { "begin", "save", "rollback" }, data.Events);
        data.Transaction.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompetingTransactionsInOneScope_RejectSecondBegin_AndRollbackFirstEvenIfRejectionIsCaught()
    {
        // Arrange
        await using Fixture data = new();
        await using AsyncServiceScope scope = data.Provider.CreateAsyncScope();
        IReceiptCreationUnitOfWork first = scope.ServiceProvider.GetRequiredService<IReceiptCreationUnitOfWork>();
        IReceiptRefreshUnitOfWork second = scope.ServiceProvider.GetRequiredService<IReceiptRefreshUnitOfWork>();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Act
        Task transaction = first.ExecuteInTransaction(async _ =>
        {
            entered.SetResult();
            await release.Task;
        }, IsolationLevel.Serializable, CancellationToken.None);
        await entered.Task;
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                second.ExecuteInTransaction(_ => Task.CompletedTask, CancellationToken.None));
        }
        finally
        {
            release.SetResult();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.SaveChangesAsync(CancellationToken.None));

        // Assert
        Assert.Equal(new[] { "begin", "rollback" }, data.Events);
        data.Connection.Protected().Verify("BeginDbTransactionAsync", Times.Once(),
            IsolationLevel.Serializable, CancellationToken.None);
    }

    private static Receipt CreateReceipt()
    {
        Assert.True(Receipt.TryCreate("fn", "fd", Guid.NewGuid().ToString("N"), new DateTime(2026, 1, 1),
            ReceiptOperationType.Income, 100m, Guid.NewGuid(), DateTime.UtcNow,
            out Receipt? receipt, out string? error), error);

        receipt!.Id = Guid.NewGuid();

        return receipt!;
    }

    private static ServiceCollection CreateRegistrations()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MSSql"] = "Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True",
            ["ApiEndpoints:ProverkachekaApiUrl"] = "https://unused.invalid",
            ["ProverkachekaApiToken"] = "unused-test-value"
        }).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        return services;
    }

    private class Fixture : IAsyncDisposable
    {
        public List<string> Events { get; } = [];
        public Mock<DbConnection> Connection { get; } = new();
        public Mock<DbTransaction> Transaction { get; } = new();
        public ServiceProvider Provider { get; }
        public string? FailureStage { get; set; }
        public Exception Failure { get; set; } = new InvalidOperationException("storage failure");

        public Fixture()
        {
            Connection.SetupGet(item => item.State).Returns(ConnectionState.Open);
            Connection.SetupGet(item => item.ConnectionString).Returns("Server=unused;Database=unused");
            Connection.Protected().Setup<ValueTask<DbTransaction>>("BeginDbTransactionAsync",
                    ItExpr.IsAny<IsolationLevel>(), ItExpr.IsAny<CancellationToken>())
                .Returns((IsolationLevel isolation, CancellationToken ct) =>
                {
                    Events.Add("begin");
                    if (FailureStage == "begin")
                    {
                        throw Failure;
                    }

                    return new ValueTask<DbTransaction>(Transaction.Object);
                });
            Transaction.Protected().Setup<DbConnection>("DbConnection").Returns(Connection.Object);
            Transaction.Setup(item => item.CommitAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) =>
            {
                Events.Add("commit");
                if (FailureStage == "commit")
                {
                    throw Failure;
                }

                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
            Transaction.Setup(item => item.RollbackAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) =>
            {
                Events.Add("rollback");
                return Task.CompletedTask;
            });
            ServiceCollection services = CreateRegistrations();
            services.AddScoped<ApplicationContext>(_ => new RecordingContext(
                new DbContextOptionsBuilder<ApplicationContext>().UseSqlServer(Connection.Object).Options, this));
            Provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
        }
    }

    private class RecordingContext(DbContextOptions<ApplicationContext> options, Fixture data) : ApplicationContext(options)
    {
        public List<CancellationToken> SaveTokens { get; } = [];
        private readonly HashSet<MoneyMovement> assignedIds = [];

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            // Только наблюдение actual DLL SaveChanges в изолированном тесте; SQL не исполняется.
            data.Events.Add("save");
            SaveTokens.Add(ct);
            if (data.FailureStage == "save")
            {
                throw data.Failure;
            }

            ct.ThrowIfCancellationRequested();
            foreach (MoneyMovement movement in ChangeTracker.Entries<MoneyMovement>().Select(entry => entry.Entity))
            {
                if (assignedIds.Add(movement))
                {
                    // Имитируем generated ID первого сохранения, не заявляя SQL/materialization-приёмку.
                    movement.Id = Guid.NewGuid();
                }
            }

            return Task.FromResult(1);
        }
    }
}
