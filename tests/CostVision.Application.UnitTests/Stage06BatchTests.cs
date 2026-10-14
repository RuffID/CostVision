using CostVision.Tests;
using System.Data;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests;

public class Stage06BatchTests
{
    [Theory]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("unexpected")]
    [InlineData("partial")]
    [InlineData("retry-write")]
    [InlineData("catalog-conflict")]
    public async Task FirstDocumentFailure_DoesNotBlockNextOrSaveItsPartialCatalog(string failure)
    {
        // Arrange
        await using Fixture data = new(2, failure);

        // Act
        await data.ExecuteAsync(CancellationToken.None);

        // Assert
        Receipt first = data.Receipts.First();
        Receipt next = data.Receipts.Last();
        Assert.Equal(ReceiptRefreshStatus.Completed, next.RefreshStatus);
        Assert.Equal(1, next.RefreshAttemptCount);
        Assert.Equal(new[] { first.Id, next.Id }, data.ProviderCalls);
        Assert.Equal(ReceiptRefreshStatus.Pending, first.RefreshStatus);
        if (failure == "http" || failure == "timeout")
        {
            Assert.Equal(1, first.RefreshAttemptCount);
            Assert.NotNull(first.LastRefreshError);
        }
        else if (failure == "retry-write")
        {
            Assert.Equal(0, first.RefreshAttemptCount);
            data.Logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(2));
        }
        else if (failure == "catalog-conflict")
        {
            Assert.Equal(1, first.RefreshAttemptCount);
            Assert.NotNull(first.LastRefreshError);
            data.Logger.Verify(log => log.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
        else
        {
            Assert.Equal(1, first.RefreshAttemptCount);
            Assert.NotNull(first.LastRefreshError);
            data.Logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        Assert.Equal("SuccessStore", Assert.Single(data.SavedStores).Name);
        Assert.Equal("Common", Assert.Single(data.SavedProducts).Name);
        Assert.Equal(0, data.ActiveScopes);
        Assert.Equal(1, data.MaximumActiveScopes);
        Assert.Equal(data.CreatedScopes, data.DisposedScopes);
        if (failure == "partial" || failure == "retry-write" || failure == "catalog-conflict")
        {
            Assert.Equal(1, data.DiscardedStores);
        }
    }

    [Fact]
    public async Task UnknownFailures_StopAfterSevenRegisteredAttemptsInFreshScopes()
    {
        // Arrange
        await using Fixture data = new(2, "unexpected");

        // Act
        foreach (int day in Enumerable.Range(0, 7))
        {
            await data.ExecuteAsync(CancellationToken.None);
            data.AdvanceDay();
        }

        int callsBeforeEighthRun = data.ProviderCalls.Count;
        await data.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.Equal(7, data.Receipts.First().RefreshAttemptCount);
        Assert.Equal(ReceiptRefreshStatus.Failed, data.Receipts.First().RefreshStatus);
        Assert.Null(data.Receipts.First().NextRefreshAttemptAtUtc);
        Assert.Equal(8, callsBeforeEighthRun);
        Assert.Equal(callsBeforeEighthRun, data.ProviderCalls.Count);
        Assert.Equal(ReceiptRefreshStatus.Completed, data.Receipts.Last().RefreshStatus);
        Assert.Equal(0, data.ActiveScopes);
        Assert.Equal(data.CreatedScopes, data.DisposedScopes);
    }

    [Fact]
    public async Task MultipleBatches_UseBoundedOrderedQueriesAndReleaseEveryScope()
    {
        // Arrange
        await using Fixture data = new(35, "none");
        Receipt future = TestReceiptFactory.Create();
        Assert.True(future.TryRegisterRefreshFailure("not due", data.Now, data.Now.AddDays(1), 7, out _));
        data.Receipts.Add(future);
        Receipt completed = TestReceiptFactory.Create();
        Assert.True(completed.TryCompleteRefresh(data.Now, false, out _));
        data.Receipts.Add(completed);

        // Act
        await data.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.Equal(new[] { 32, 3, 0 }, data.BatchSizes);
        Assert.Equal(35, data.ProviderCalls.Count);
        Assert.Equal(35, data.ProviderCalls.Distinct().Count());
        Assert.Equal(data.Receipts.Take(35).Select(item => item.Id), data.ProviderCalls);
        Assert.DoesNotContain(future.Id, data.ProviderCalls);
        Assert.DoesNotContain(completed.Id, data.ProviderCalls);
        Assert.Equal(38, data.CreatedScopes);
        Assert.Equal(38, data.DisposedScopes);
        Assert.Equal(0, data.ActiveScopes);
        Assert.Equal(1, data.MaximumActiveScopes);
        Assert.Single(data.SavedProducts);
        Assert.Single(data.SavedStores);
    }

    [Fact]
    public async Task Cancellation_StopsPassWithoutAttemptAndReleasesScope()
    {
        // Arrange
        await using Fixture data = new(2, "cancel");
        using CancellationTokenSource cancellation = new();
        data.Cancellation = cancellation;

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => data.ExecuteAsync(cancellation.Token));

        // Assert
        Assert.Single(data.ProviderCalls);
        Assert.All(data.Receipts, item => Assert.Equal(0, item.RefreshAttemptCount));
        Assert.Empty(data.SavedStores);
        Assert.Empty(data.SavedProducts);
        Assert.Equal(data.CreatedScopes, data.DisposedScopes);
        Assert.Equal(0, data.ActiveScopes);
    }

    private class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly FastClock clock;
        public DateTime Now { get; } = new(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
        public List<Receipt> Receipts { get; } = [];
        public List<Store> SavedStores { get; } = [];
        public List<Product> SavedProducts { get; } = [];
        public List<Guid> ProviderCalls { get; } = [];
        public List<int> BatchSizes { get; } = [];
        public Mock<ILogger<RefreshPendingReceiptsUseCase>> Logger { get; } = new();
        public string Failure { get; }
        public CancellationTokenSource? Cancellation { get; set; }
        public int ActiveScopes { get; set; }
        public int MaximumActiveScopes { get; set; }
        public int CreatedScopes { get; set; }
        public int DisposedScopes { get; set; }
        public int DiscardedStores { get; set; }

        public Fixture(int count, string failure)
        {
            Failure = failure;
            Receipts.AddRange(Enumerable.Range(0, count).Select(_ => TestReceiptFactory.Create()));
            Receipts.Sort((left, right) => left.Id.CompareTo(right.Id));
            Mock<IExternalReceiptProvider> provider = new(MockBehavior.Strict);
            provider.Setup(item => item.GetReceiptAsync(It.IsAny<Receipt>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Receipt snapshot, CancellationToken ct) =>
                {
                    ProviderCalls.Add(snapshot.Id);
                    Assert.Equal(1, ActiveScopes);
                    if (snapshot.Id == Receipts.First().Id)
                    {
                        if (Failure == "http")
                        {
                            throw new HttpRequestException("connection");
                        }
                        else if (Failure == "timeout")
                        {
                            throw new TaskCanceledException("timeout");
                        }
                        else if (Failure == "unexpected")
                        {
                            throw new InvalidOperationException("unknown provider error");
                        }
                        else if (Failure == "cancel")
                        {
                            Cancellation!.Cancel();
                            ct.ThrowIfCancellationRequested();
                        }
                    }

                    Receipt external = Copy(snapshot);
                    string storeName = "SuccessStore";
                    if ((Failure == "partial" || Failure == "retry-write" || Failure == "catalog-conflict") && snapshot.Id == Receipts.First().Id)
                    {
                        storeName = "FailStore";
                    }

                    external.AssignStore(TestReceiptFactory.CreateStore(storeName));
                    Assert.True(external.TryAddItem(TestReceiptFactory.CreateItem(TestReceiptFactory.CreateProduct("Common")), out _));
                    return ServiceResult<Receipt>.Ok(external);
                });
            ServiceCollection registrations = new();
            clock = new FastClock(Now);
            registrations.AddSingleton<TimeProvider>(clock);
            registrations.AddSingleton(provider.Object);
            registrations.AddScoped(_ => new Context(this));
            registrations.AddScoped<IReceiptRepository>(scope => scope.GetRequiredService<Context>().Work.Object.Receipt);
            registrations.AddScoped<IReceiptRefreshWorkflow>(scope => TestUseCaseFactory.CreateReceiptRefreshWorkflow(
                scope.GetRequiredService<Context>().Work.Object, scope.GetRequiredService<IExternalReceiptProvider>(), scope.GetRequiredService<TimeProvider>()));
            services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            RefreshPendingReceiptsUseCase useCase = new(services.GetRequiredService<IServiceScopeFactory>(),
                Logger.Object, services.GetRequiredService<TimeProvider>());

            await useCase.ExecuteAsync(ct);
        }

        public async ValueTask DisposeAsync()
        {
            await services.DisposeAsync();
        }

        public void AdvanceDay()
        {
            clock.AdvanceDay();
        }

        private static Receipt Copy(Receipt receipt)
        {
            Receipt copy = TestReceiptFactory.Create(receipt.Id, receipt.CreatedByUserId, receipt.DateTime, receipt.TotalSum,
                receipt.OperationType, fiscalDriveNumber: receipt.FiscalDriveNumber,
                fiscalDocumentNumber: receipt.FiscalDocumentNumber, fiscalSign: receipt.FiscalSign);
            // Только имитация detached/tracked материализации в изолированной модели хранения.
            foreach (string name in new[] { nameof(Receipt.RefreshStatus), nameof(Receipt.RefreshAttemptCount),
                nameof(Receipt.LastRefreshAttemptAtUtc), nameof(Receipt.NextRefreshAttemptAtUtc), nameof(Receipt.LastRefreshError) })
            {
                System.Reflection.PropertyInfo property = typeof(Receipt).GetProperty(name)!;
                property.SetValue(copy, property.GetValue(receipt));
            }

            return copy;
        }

        private class Context : IAsyncDisposable
        {
            private readonly Fixture data;
            private readonly List<Store> pendingStores = [];
            private readonly List<Product> pendingProducts = [];
            private bool saved;
            private bool failed;
            private Guid trackedReceiptId;
            private Receipt? trackedReceipt;
            public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);

            public Context(Fixture data)
            {
                this.data = data;
                data.CreatedScopes++;
                data.ActiveScopes++;
                data.MaximumActiveScopes = Math.Max(data.MaximumActiveScopes, data.ActiveScopes);
                Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
                receipts.Setup(item => item.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(), 0,
                        RefreshPendingReceiptsUseCase.BATCH_SIZE, true,
                        It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool noTracking,
                        Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                    {
                        List<Receipt> batch = include(data.Receipts.AsQueryable().Where(predicate)).Take(take!.Value).Select(Copy).ToList();
                        data.BatchSizes.Add(batch.Count);
                        return batch;
                    });
                receipts.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(), false,
                        It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, bool noTracking,
                        Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                    {
                        Receipt? receipt = data.Receipts.FirstOrDefault(predicate.Compile());
                        if (receipt != null)
                        {
                            trackedReceiptId = receipt.Id;
                            trackedReceipt = Copy(receipt);
                        }

                        return trackedReceipt;
                    });
                Mock<IStoreRepository> stores = new(MockBehavior.Strict);
                stores.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<Store, bool>>>(), false,
                        It.IsAny<Func<IQueryable<Store>, IQueryable<Store>>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Expression<Func<Store, bool>> predicate, bool noTracking,
                        Func<IQueryable<Store>, IQueryable<Store>>? include, CancellationToken ct) =>
                        data.SavedStores.FirstOrDefault(predicate.Compile()));
                stores.Setup(item => item.Create(It.IsAny<Store>())).Callback<Store>(pendingStores.Add);
                Mock<IProductRepository> products = new(MockBehavior.Strict);
                products.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(), false,
                        It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Expression<Func<Product, bool>> predicate, bool noTracking,
                        Func<IQueryable<Product>, IQueryable<Product>>? include, CancellationToken ct) =>
                    {
                        if (data.Failure != "catalog-conflict" && pendingStores.Any(store => store.Name == "FailStore"))
                        {
                            throw new InvalidOperationException("failed after store Create");
                        }

                        return data.SavedProducts.FirstOrDefault(predicate.Compile());
                    });
                products.Setup(item => item.Create(It.IsAny<Product>())).Callback<Product>(pendingProducts.Add);
                Work.SetupGet(item => item.Receipt).Returns(receipts.Object);
                Work.SetupGet(item => item.Store).Returns(stores.Object);
                Work.SetupGet(item => item.Product).Returns(products.Object);
                Work.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) =>
                {
                    Assert.False(failed);
                    ct.ThrowIfCancellationRequested();
                    if (data.Failure == "retry-write" && trackedReceiptId == data.Receipts.First().Id && pendingStores.Count == 0)
                    {
                        throw new InvalidOperationException("retry-state storage failure");
                    }

                    if (data.Failure == "catalog-conflict" && pendingStores.Any(store => store.Name == "FailStore"))
                    {
                        throw new ReceiptWriteConflictException("catalog unique conflict", new InvalidOperationException("normalized index"));
                    }

                    saved = true;
                    return Task.CompletedTask;
                });
                Work.Setup(item => item.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(),
                        IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
                    .Returns(async (Func<CancellationToken, Task> action, IsolationLevel isolation, CancellationToken ct) =>
                    {
                        try
                        {
                            await action(ct);
                            if (saved)
                            {
                                int index = data.Receipts.FindIndex(item => item.Id == trackedReceiptId);
                                Assert.True(index >= 0);
                                data.Receipts[index] = trackedReceipt!;
                                data.SavedStores.AddRange(pendingStores);
                                data.SavedProducts.AddRange(pendingProducts);
                            }
                        }
                        catch
                        {
                            failed = true;
                            throw;
                        }
                    });
            }

            public ValueTask DisposeAsync()
            {
                data.ActiveScopes--;
                data.DisposedScopes++;
                if (!saved)
                {
                    data.DiscardedStores += pendingStores.Count;
                }

                return ValueTask.CompletedTask;
            }
        }

        private class FastClock(DateTime now) : TimeProvider
        {
            private DateTime currentTime = now;

            public override DateTimeOffset GetUtcNow()
            {
                return new DateTimeOffset(currentTime);
            }

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

            public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            {
                return System.CreateTimer(callback, state, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            }

            public void AdvanceDay()
            {
                currentTime = currentTime.AddDays(1);
            }
        }
    }
}
