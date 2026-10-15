using CostVision.Tests;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;
using System.Data;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using Microsoft.EntityFrameworkCore;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.UnitTests;

public class ReceiptRefreshConsistencyTests
{
    [Theory]
    [InlineData("fn")]
    [InlineData("fd")]
    [InlineData("fp")]
    [InlineData("time")]
    [InlineData("sum")]
    [InlineData("operation")]
    public void Domain_RejectsDifferentIdentityBeforeMutation(string field)
    {
        // Arrange
        Receipt receipt = TestReceiptFactory.Create();
        Store store = TestReceiptFactory.CreateStore("Original");
        receipt.AssignStore(store);
        ReceiptItem item = TestReceiptFactory.CreateItem(TestReceiptFactory.CreateProduct("Original"));
        Assert.True(receipt.TryAddItem(item, out _));
        Assert.True(receipt.TryAddAccount(Guid.NewGuid(), out ReceiptAccount? account, out _));
        TestReceiptFactory.AddMoneyMovementLink(receipt);
        Receipt source = DifferentIdentity(receipt, field);
        ReceiptIdentity identity = receipt.GetIdentity();

        // Act
        bool result = receipt.TryRefreshFrom(source, null, [], DateTime.UtcNow, out string? error);

        // Assert
        Assert.False(result);
        Assert.NotNull(error);
        Assert.Equal(identity, receipt.GetIdentity());
        Assert.Same(store, receipt.Store);
        Assert.Same(item, Assert.Single(receipt.Items));
        Assert.Same(account, Assert.Single(receipt.Accounts));
        Assert.Single(receipt.MoneyMovementLinks);
        Assert.Null(receipt.UpdatedAtUtc);
        Assert.Equal(0, receipt.RefreshRevision);
    }

    [Fact]
    public void IdenticalGraphAndTimestamp_StillAdvancesReceiptWriteRevision()
    {
        // Arrange
        Receipt receipt = TestReceiptFactory.Create();
        Receipt source = TestReceiptFactory.Create();
        Product product = TestReceiptFactory.CreateProduct("Milk");
        DateTime sameTimestamp = new(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
        ReceiptItem first = TestReceiptFactory.CreateItem(product);
        ReceiptItem second = TestReceiptFactory.CreateItem(product);

        // Act
        Assert.True(receipt.TryRefreshFrom(source, null, [first], sameTimestamp, out _));
        long firstRevision = receipt.RefreshRevision;
        Assert.True(receipt.TryRefreshFrom(source, null, [second], sameTimestamp, out _));

        // Assert
        Assert.Equal(1, firstRevision);
        Assert.Equal(2, receipt.RefreshRevision);
        Assert.Equal(sameTimestamp, receipt.UpdatedAtUtc);
        Assert.Same(second, Assert.Single(receipt.Items));
        Assert.Equal(source.GetIdentity(), receipt.GetIdentity());
    }

    [Fact]
    public async Task Workflow_RejectsDifferentIdentityWithoutReadingOrWritingCatalogs()
    {
        // Arrange
        Receipt receipt = TestReceiptFactory.Create();
        Mock<ITestStorage> unitOfWork = new(MockBehavior.Strict);
        Mock<IExternalReceiptProvider> provider = new(MockBehavior.Strict);
        provider.Setup(value => value.GetReceiptAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(DifferentIdentity(receipt, "fn")));
        ReceiptRefreshWorkflow workflow = TestUseCaseFactory.CreateReceiptRefreshWorkflow(unitOfWork.Object, provider.Object, TimeProvider.System);

        // Act
        ServiceResult<Receipt> result = await workflow.RefreshAsync(receipt, CancellationToken.None, receipt.CreatedByUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.ExternalService, result.Error.Type);
        unitOfWork.VerifyNoOtherCalls();
        Assert.Null(receipt.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("transport-timeout")]
    public async Task Scheduled_RecordsTemporaryFailureWithoutCatalogMutation(string kind)
    {
        // Arrange
        Fixture data = new();
        Exception exception = new HttpRequestException("transport");
        if (kind == "timeout")
        {
            exception = new TimeoutException();
        }
        else if (kind == "transport-timeout")
        {
            exception = new TaskCanceledException();
        }

        data.Provider.Setup(item => item.GetReceiptAsync(data.Snapshot, It.IsAny<CancellationToken>())).ThrowsAsync(exception);

        // Act
        ServiceResult<Receipt> result = await data.Workflow.RefreshScheduledAsync(data.Snapshot, data.Now, data.Now.AddDays(1), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.ExternalService, result.Error.Type);
        Assert.Equal(1, data.Current.RefreshAttemptCount);
        Assert.Equal(data.Now.AddDays(1), data.Current.NextRefreshAttemptAtUtc);
        Assert.Equal(1, data.Saves);
        Assert.Empty(data.SavedProducts);
        Assert.Empty(data.SavedStores);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_DoesNotRegisterAttemptOrSave(bool afterResponse)
    {
        // Arrange
        Fixture data = new();
        using CancellationTokenSource cancellation = new();
        if (afterResponse)
        {
            data.Provider.Setup(item => item.GetReceiptAsync(data.Snapshot, It.IsAny<CancellationToken>()))
                .Callback(() => cancellation.Cancel()).ReturnsAsync(ServiceResult<Receipt>.Ok(data.External()));
        }
        else
        {
            cancellation.Cancel();
        }

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => data.Workflow.RefreshScheduledAsync(
            data.Snapshot, data.Now, data.Now.AddDays(1), cancellation.Token));

        // Assert
        Assert.Equal(0, data.Current.RefreshAttemptCount);
        Assert.Null(data.Current.LastRefreshError);
        data.Work.VerifyNoOtherCalls();
        Assert.Equal(0, data.Saves);
    }

    [Fact]
    public async Task InvalidExternalProduct_IsRejectedBeforeAnyCatalogMutation()
    {
        // Arrange
        Fixture data = new();
        Receipt external = data.External("Milk");
        // Имитируется повреждённая материализация стороннего provider, без ослабления production API.
        typeof(Product).GetProperty(nameof(Product.Name))!.SetValue(external.Items.Single().Product, "");
        data.Provider.Setup(item => item.GetReceiptAsync(data.Snapshot, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(external));

        // Act
        ServiceResult<Receipt> result = await data.Workflow.RefreshScheduledAsync(data.Snapshot, data.Now, data.Now.AddDays(1), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(1, data.Current.RefreshAttemptCount);
        Assert.Empty(data.PendingStores);
        Assert.Empty(data.PendingProducts);
        Assert.Empty(data.Current.Items);
        Assert.Null(data.Current.Store);
        Assert.Equal(1, data.Saves);
    }

    [Fact]
    public async Task CancellationDuringApplication_RollsBackPartialGraphWithoutRetrySave()
    {
        // Arrange
        Fixture data = new();
        using CancellationTokenSource cancellation = new();
        data.ReturnExternal("Milk");
        data.BeforeProductQuery = cancellation.Cancel;

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => data.Workflow.RefreshScheduledAsync(
            data.Snapshot, data.Now, data.Now.AddDays(1), cancellation.Token));

        // Assert
        Assert.Single(data.PendingStores);
        Assert.Empty(data.SavedStores);
        Assert.Empty(data.SavedProducts);
        Assert.Equal(0, data.Current.RefreshAttemptCount);
        Assert.Equal(0, data.Saves);
        Assert.Equal(1, data.Rollbacks);
    }

    [Fact]
    public async Task UnknownProviderException_IsPropagatedWithoutRetryOrWrite()
    {
        // Arrange
        Fixture data = new();
        InvalidOperationException failure = new("unexpected");
        data.Provider.Setup(item => item.GetReceiptAsync(data.Snapshot, It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        // Act
        Exception thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => data.Workflow.RefreshScheduledAsync(
            data.Snapshot, data.Now, data.Now.AddDays(1), CancellationToken.None));

        // Assert
        Assert.Same(failure, thrown);
        Assert.Equal(0, data.Saves);
        Assert.Equal(0, data.Current.RefreshAttemptCount);
        data.Work.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ApplicationFailure_DiscardsPartialCatalogAndBlocksReuse()
    {
        // Arrange
        Fixture data = new();
        data.ReturnExternal("Milk");
        data.ProductQueryFailure = new InvalidOperationException("storage query failed after store Create");

        // Act
        Exception thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => data.Workflow.RefreshScheduledAsync(
            data.Snapshot, data.Now, data.Now.AddDays(1), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => data.Work.Object.SaveChangesAsync(CancellationToken.None));

        // Assert
        Assert.Same(data.ProductQueryFailure, thrown);
        Assert.Single(data.PendingStores);
        Assert.Empty(data.SavedStores);
        Assert.Empty(data.SavedProducts);
        Assert.Equal(0, data.Saves);
        Assert.Equal(1, data.Rollbacks);
        Assert.Equal(0, data.Current.RefreshAttemptCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleVersion_RejectsSuccessAndRetryWithoutMutation(bool providerFailure)
    {
        // Arrange
        Fixture data = new();
        typeof(Receipt).GetProperty(nameof(Receipt.RowVersion))!.SetValue(data.Current, new byte[] { 2 });
        if (providerFailure)
        {
            data.Provider.Setup(item => item.GetReceiptAsync(data.Snapshot, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "unavailable"));
        }
        else
        {
            data.ReturnExternal("Milk");
        }

        // Act
        ServiceResult<Receipt> result = await data.Workflow.RefreshScheduledAsync(data.Snapshot, data.Now, data.Now.AddDays(1), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.Error.Type);
        Assert.Equal(0, data.Saves);
        Assert.Equal(0, data.Current.RefreshAttemptCount);
        Assert.Empty(data.PendingStores);
        Assert.Empty(data.PendingProducts);
    }

    [Fact]
    public async Task RowVersionConflictAtSave_ReturnsConflictWithoutSecondSave()
    {
        // Arrange
        Fixture data = new();
        data.ReturnExternal("Milk");
        data.SaveFailure = new DbUpdateConcurrencyException("stale rowversion");

        // Act
        ServiceResult<Receipt> result = await data.Workflow.RefreshScheduledAsync(data.Snapshot, data.Now, data.Now.AddDays(1), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.Error.Type);
        Assert.Equal(1, data.Saves);
        Assert.Equal(1, data.Rollbacks);
        Assert.Empty(data.SavedProducts);
        Assert.Empty(data.SavedStores);
    }

    [Fact]
    public async Task AccessRevokedWhileFetching_IsRecheckedBeforeMutation()
    {
        // Arrange
        Fixture data = new();
        Guid excludedUser = Guid.NewGuid();
        Account previousAccount = TestAccountFactory.Create();
        Assert.True(previousAccount.TryAddMember(excludedUser, AccountAccessRole.Viewer, out _, out _));
        Assert.True(data.Snapshot.TryAddAccount(previousAccount, out _, out _));
        User user = TestUserFactory.Create(excludedUser);
        Mock<IReceiptAccessVerificationService> access = new(MockBehavior.Strict);
        access.Setup(item => item.UserHasAccessToReceipt(user, data.Snapshot)).Returns(true);
        data.SetupSnapshotRead();
        data.ReturnExternal("Milk");

        // Act
        ServiceResult<ReceiptDto> result = await TestUseCaseFactory.CreateRefreshReceiptFromApiUseCase(data.Work.Object, access.Object, data.Workflow)
            .ExecuteAsync(data.Snapshot.Id, user, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error.Type);
        Assert.Equal(0, data.Saves);
        Assert.Empty(data.PendingProducts);
        Assert.Empty(data.PendingStores);
        data.Provider.Verify(item => item.GetReceiptAsync(data.Snapshot, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuccessfulRefresh_PreservesOwnershipLinksItemOrderAndProductCache()
    {
        // Arrange
        Fixture data = new();
        Assert.True(data.Current.TryAddAccount(Guid.NewGuid(), out ReceiptAccount? account, out _));
        TestReceiptFactory.AddMoneyMovementLink(data.Current);
        Guid author = data.Current.CreatedByUserId;
        ReceiptIdentity identity = data.Current.GetIdentity();
        data.ReturnExternal("Z", "A", "Z");

        // Act
        ServiceResult<Receipt> result = await data.Workflow.RefreshAsync(data.Snapshot, CancellationToken.None, author);

        // Assert
        Assert.True(result.Success);
        Assert.Same(data.Current, result.Data);
        Assert.Equal(author, result.Data.CreatedByUserId);
        Assert.Equal(identity, result.Data.GetIdentity());
        Assert.Same(account, Assert.Single(result.Data.Accounts));
        Assert.Single(result.Data.MoneyMovementLinks);
        Assert.Equal(new[] { "Z", "A", "Z" }, result.Data.Items.Select(item => item.Product!.Name));
        Assert.Same(result.Data.Items.First().Product, result.Data.Items.Last().Product);
        Assert.Equal(2, data.ProductQueries);
        Assert.Equal(2, data.SavedProducts.Count);
        Assert.Single(data.SavedStores);
        Assert.Equal(0, result.Data.RefreshAttemptCount);
        Assert.Equal(ReceiptRefreshStatus.Completed, result.Data.RefreshStatus);
    }

    [Fact]
    public async Task ManualRefresh_RecoversFailedReceiptWithoutIncreasingSevenAttempts()
    {
        // Arrange
        Fixture data = new();
        foreach (Receipt receipt in new[] { data.Snapshot, data.Current })
        {
            foreach (int day in Enumerable.Range(0, 7))
            {
                DateTime attempted = data.Now.AddDays(day - 7);
                Assert.True(receipt.TryRegisterRefreshFailure("old error", attempted, attempted.AddDays(1), 7, out _));
            }
        }

        data.ReturnExternal("Milk");

        // Act
        ServiceResult<Receipt> result = await data.Workflow.RefreshAsync(data.Snapshot, CancellationToken.None, data.Current.CreatedByUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(7, result.Data.RefreshAttemptCount);
        Assert.Equal(ReceiptRefreshStatus.Completed, result.Data.RefreshStatus);
        Assert.Null(result.Data.LastRefreshError);
        Assert.Null(result.Data.NextRefreshAttemptAtUtc);
    }

    [Fact]
    public async Task ActualManualRefresh_ReturnsDistinctAccessibleAggregates()
    {
        // Arrange
        Fixture data = new();
        User user = TestUserFactory.Create(data.Snapshot.CreatedByUserId);
        Receipt copy = TestReceiptFactory.Create();
        Account shared = TestAccountFactory.Create(ownerUserId: user.Id);
        Assert.True(copy.TryAddAccount(shared, out _, out _));
        MoneyMovement common = TestMoneyMovementFactory.CreateManual(userId: user.Id, amount: 40m);
        MoneyMovement second = TestMoneyMovementFactory.CreateManual(userId: user.Id, amount: 60m);
        MoneyMovement hidden = TestMoneyMovementFactory.CreateManual(amount: 9000m);
        TestReceiptFactory.AddMoneyMovementLink(data.Current, common);
        TestReceiptFactory.AddMoneyMovementLink(copy, common);
        TestReceiptFactory.AddMoneyMovementLink(copy, second);
        TestReceiptFactory.AddMoneyMovementLink(data.Current, hidden);
        data.SetupSnapshotRead();
        data.ReceiptRepository.Setup(item => item.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                0, null, true, It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool noTracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                new[] { data.Current, copy }.Where(predicate.Compile()).ToList());
        Mock<IReceiptAccessVerificationService> access = new(MockBehavior.Strict);
        access.Setup(item => item.UserHasAccessToReceipt(user, data.Snapshot)).Returns(true);
        data.ReturnExternal("Milk");

        // Act
        ServiceResult<ReceiptDto> result = await TestUseCaseFactory.CreateRefreshReceiptFromApiUseCase(data.Work.Object, access.Object, data.Workflow)
            .ExecuteAsync(data.Snapshot.Id, user, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Data.MoneyMovementCount);
        Assert.Equal(100m, result.Data.MoneyMovementsTotalSum);
        Assert.Equal(user.Id, data.Current.CreatedByUserId);
        Assert.Equal(data.Snapshot.GetIdentity(), data.Current.GetIdentity());
        Assert.Equal(2, data.Current.MoneyMovementLinks.Count);
        Assert.Empty(data.Snapshot.Items);
        Assert.Single(result.Data.Items);
    }

    [Theory]
    [InlineData(16, 59, 59, 13)]
    [InlineData(17, 0, 0, 14)]
    [InlineData(17, 0, 1, 14)]
    public void Policy_StoresNextLocalMidnightInUtc(int hour, int minute, int second, int expectedDay)
    {
        // Arrange
        TimeProvider clock = new FixedClock(new DateTimeOffset(2026, 10, 13, hour, minute, second, TimeSpan.Zero),
            TimeZoneInfo.CreateCustomTimeZone("TestUTC7", TimeSpan.FromHours(7), "UTC7", "UTC7"));

        // Act
        DateTime next = ReceiptRefreshPolicy.GetNextDailyRunUtc(clock);

        // Assert
        Assert.Equal(new DateTime(2026, 10, expectedDay, 17, 0, 0, DateTimeKind.Utc), next);
        Assert.Equal(DateTimeKind.Utc, next.Kind);
    }

    private class FixedClock(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }

        public override TimeZoneInfo LocalTimeZone => zone;
    }

    private class Fixture
    {
        public DateTime Now { get; } = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        public Receipt Snapshot { get; } = TestReceiptFactory.Create();
        public Receipt Current { get; }
        public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);
        public Mock<IExternalReceiptProvider> Provider { get; } = new(MockBehavior.Strict);
        public Mock<IReceiptRepository> ReceiptRepository { get; } = new(MockBehavior.Strict);
        public ReceiptRefreshWorkflow Workflow { get; }
        public List<Product> PendingProducts { get; } = [];
        public List<Store> PendingStores { get; } = [];
        public List<Product> SavedProducts { get; } = [];
        public List<Store> SavedStores { get; } = [];
        public Exception? ProductQueryFailure { get; set; }
        public Action? BeforeProductQuery { get; set; }
        public Exception? SaveFailure { get; set; }
        public int ProductQueries { get; private set; }
        public int Saves { get; private set; }
        public int Rollbacks { get; private set; }
        private bool inTransaction;
        private bool poisoned;

        public Fixture()
        {
            Current = TestReceiptFactory.Create(Snapshot.Id, Snapshot.CreatedByUserId);
            typeof(Receipt).GetProperty(nameof(Receipt.RowVersion))!.SetValue(Snapshot, new byte[] { 1 });
            typeof(Receipt).GetProperty(nameof(Receipt.RowVersion))!.SetValue(Current, new byte[] { 1 });
            Mock<IReceiptRepository> receipts = ReceiptRepository;
            receipts.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(), false,
                    It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, bool noTracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                {
                    Assert.True(inTransaction);
                    return new[] { Current }.FirstOrDefault(predicate.Compile());
                });
            Mock<IProductRepository> products = new(MockBehavior.Strict);
            products.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(), false,
                    It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Product, bool>> predicate, bool noTracking,
                    Func<IQueryable<Product>, IQueryable<Product>>? include, CancellationToken ct) =>
                {
                    Assert.True(inTransaction);
                    ProductQueries++;
                    BeforeProductQuery?.Invoke();
                    ct.ThrowIfCancellationRequested();
                    if (ProductQueryFailure != null)
                    {
                        throw ProductQueryFailure;
                    }

                    return SavedProducts.FirstOrDefault(predicate.Compile());
                });
            products.Setup(item => item.Create(It.IsAny<Product>())).Callback<Product>(PendingProducts.Add);
            Mock<IStoreRepository> stores = new(MockBehavior.Strict);
            stores.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<Store, bool>>>(), false,
                    It.IsAny<Func<IQueryable<Store>, IQueryable<Store>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Store, bool>> predicate, bool noTracking,
                    Func<IQueryable<Store>, IQueryable<Store>>? include, CancellationToken ct) =>
                    SavedStores.FirstOrDefault(predicate.Compile()));
            stores.Setup(item => item.Create(It.IsAny<Store>())).Callback<Store>(PendingStores.Add);
            Work.SetupGet(item => item.Receipt).Returns(receipts.Object);
            Work.SetupGet(item => item.Product).Returns(products.Object);
            Work.SetupGet(item => item.Store).Returns(stores.Object);
            Work.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken ct) =>
            {
                if (poisoned)
                {
                    throw new InvalidOperationException("New scope required");
                }

                Assert.True(inTransaction);
                ct.ThrowIfCancellationRequested();
                Saves++;
                if (SaveFailure != null)
                {
                    throw SaveFailure;
                }

                return Task.CompletedTask;
            });
            Work.Setup(item => item.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(),
                    IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
                .Returns(async (Func<CancellationToken, Task> action, IsolationLevel isolation, CancellationToken ct) =>
                {
                    inTransaction = true;
                    try
                    {
                        await action(ct);
                        SavedProducts.AddRange(PendingProducts);
                        SavedStores.AddRange(PendingStores);
                    }
                    catch
                    {
                        poisoned = true;
                        Rollbacks++;
                        throw;
                    }
                    finally
                    {
                        inTransaction = false;
                    }
                });
            Workflow = TestUseCaseFactory.CreateReceiptRefreshWorkflow(Work.Object, Provider.Object, new FixedClock(new DateTimeOffset(Now), TimeZoneInfo.Utc));
        }

        public Receipt External(params string[] names)
        {
            Receipt receipt = TestReceiptFactory.Create(createdByUserId: Snapshot.CreatedByUserId);
            receipt.AssignStore(TestReceiptFactory.CreateStore("Shop"));
            foreach (string name in names)
            {
                Assert.True(receipt.TryAddItem(TestReceiptFactory.CreateItem(TestReceiptFactory.CreateProduct(name)), out _));
            }

            return receipt;
        }

        public void ReturnExternal(params string[] names)
        {
            Provider.Setup(item => item.GetReceiptAsync(Snapshot, It.IsAny<CancellationToken>()))
                .Callback(() => Assert.False(inTransaction)).ReturnsAsync(ServiceResult<Receipt>.Ok(External(names)));
        }

        public void SetupSnapshotRead()
        {
            ReceiptRepository.Setup(item => item.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(), true,
                    It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, bool noTracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                    new[] { Snapshot }.FirstOrDefault(predicate.Compile()));
        }
    }

    private static Receipt DifferentIdentity(Receipt receipt, string field)
    {
        string fn = receipt.FiscalDriveNumber;
        string fd = receipt.FiscalDocumentNumber;
        string fp = receipt.FiscalSign;
        DateTime date = receipt.DateTime;
        decimal sum = receipt.TotalSum;
        ReceiptOperationType operation = receipt.OperationType;
        if (field == "fn")
        {
            fn += "1";
        }
        else if (field == "fd")
        {
            fd += "1";
        }
        else if (field == "fp")
        {
            fp += "1";
        }
        else if (field == "time")
        {
            date = date.AddSeconds(1);
        }
        else if (field == "sum")
        {
            sum += 0.01m;
        }
        else
        {
            operation = ReceiptOperationType.RefundIncome;
        }

        return TestReceiptFactory.Create(dateTime: date, totalSum: sum, operationType: operation,
            fiscalDriveNumber: fn, fiscalDocumentNumber: fd, fiscalSign: fp);
    }
}
