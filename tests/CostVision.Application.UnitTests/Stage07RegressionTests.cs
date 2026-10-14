using CostVision.Tests;
using System.Linq.Expressions;
using System.Reflection;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests;

public class Stage07RegressionTests
{
    [Fact]
    public async Task MoneyMovements_SearchBeforePageHasFullTotalsAndCountsOnlyDisplayedRows()
    {
        // Arrange
        Fixture data = new();
        data.Movements.Add(TestMoneyMovementFactory.CreateManual(Guid.Parse("00000000-0000-0000-0000-000000000002"),
            data.Account.Id, 50m, data.Date, data.UserId, type: MoneyMovementType.Income, comment: "match"));
        data.Movements.Add(TestMoneyMovementFactory.CreateManual(Guid.Parse("00000000-0000-0000-0000-000000000001"),
            data.Account.Id, 100m, data.Date, data.UserId, type: MoneyMovementType.Expense, comment: "match"));
        data.Movements.Add(TestMoneyMovementFactory.CreateManual(accountId: data.Account.Id,
            occurredAt: data.Date.AddHours(1), userId: data.UserId, amount: 900m, comment: "excluded"));
        GetMoneyMovementListUseCase useCase = TestUseCaseFactory.CreateGetMoneyMovementListUseCase(data.Work.Object);

        // Act
        var first = await useCase.ExecuteAsync(data.UserId, data.Date, data.Date, null, CancellationToken.None,
            pageSize: 1, search: "match");
        var last = await useCase.ExecuteAsync(data.UserId, data.Date, data.Date, null, CancellationToken.None,
            page: 999, pageSize: 1, search: "match");

        // Assert
        Assert.True(first.Success);
        Assert.True(last.Success);
        Assert.Equal(data.Movements[1].Id, Assert.Single(first.Data.Items).Id);
        Assert.Equal(data.Movements[0].Id, Assert.Single(last.Data.Items).Id);
        Assert.Equal(2, first.Data.TotalCount);
        Assert.Equal(50m, first.Data.IncomeSum);
        Assert.Equal(100m, first.Data.ExpenseSum);
        Assert.Equal(first.Data.IncomeSum, last.Data.IncomeSum);
        Assert.Equal(first.Data.ExpenseSum, last.Data.ExpenseSum);
        Assert.Equal(2, last.Data.Page);
        Assert.Equal(2, data.ReceiptProjectionReads.Count);
        Assert.Equal(0, data.MovementReads);
    }

    [Fact]
    public async Task StoreGroups_StayTogetherAcrossPagesWithDistinctDocumentTotals()
    {
        // Arrange
        Fixture data = new();
        Store firstStore = data.AddStore("Chain");
        Store secondStore = data.AddStore("Chain");
        Store otherStore = data.AddStore("Other");
        data.AddPurchase(firstStore, data.AddProduct("First"), identity: "same");
        data.AddPurchase(secondStore, data.AddProduct("Copy"), identity: "same", authorId: Guid.NewGuid());
        data.AddPurchase(secondStore, data.AddProduct("Second"), identity: "second");
        data.AddPurchase(otherStore, data.AddProduct("Third"), identity: "third", type: ReceiptOperationType.RefundIncome);
        GetStoreListUseCase useCase = TestUseCaseFactory.CreateGetStoreListUseCase(data.Work.Object);

        // Act
        var first = await useCase.ExecuteAsync(new() { GroupByName = true, PageSize = 1 }, data.UserId, CancellationToken.None);
        var second = await useCase.ExecuteAsync(new() { GroupByName = true, PageSize = 1, Page = 2 }, data.UserId, CancellationToken.None);

        // Assert
        Assert.True(first.Success);
        Assert.True(second.Success);
        var group = Assert.Single(first.Data.Items);
        Assert.Equal(2, group.Children.Count);
        Assert.Equal(2, group.ReceiptCount);
        Assert.Equal(200m, group.TotalSpent);
        Assert.Equal("Other", Assert.Single(second.Data.Items).Name);
        Assert.Equal(2, first.Data.TotalCount);
        Assert.Equal(200m, first.Data.TotalSum);
        Assert.Equal(100m, first.Data.IncomeSum);
        Assert.Equal(first.Data.TotalSum, second.Data.TotalSum);
    }

    [Fact]
    public async Task ReceiptCandidates_PagesDocumentsAndKeepsAllCopiesOfSelectedDocument()
    {
        // Arrange
        Fixture data = new();
        Store store = data.AddStore("Store");
        Receipt nearest = data.AddPurchase(store, data.AddProduct("First"), identity: "a");
        Receipt copy = data.AddPurchase(store, data.AddProduct("Copy"), identity: "a", authorId: Guid.NewGuid());
        Receipt next = data.AddPurchase(store, data.AddProduct("Next"), identity: "b");
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(accountId: data.Account.Id,
            occurredAt: data.Date, userId: data.UserId);
        data.Movements.Add(movement);
        GetMoneyMovementReceiptCandidatesUseCase useCase = TestUseCaseFactory.CreateGetMoneyMovementReceiptCandidatesUseCase(data.Work.Object);

        // Act
        var first = await useCase.ExecuteAsync(new()
        {
            MoneyMovementId = movement.Id, PageSize = 1
        }, data.UserId, CancellationToken.None);
        var second = await useCase.ExecuteAsync(new()
        {
            MoneyMovementId = movement.Id, PageSize = 1, Page = 2
        }, data.UserId, CancellationToken.None);

        // Assert
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(2, first.Data.TotalCount);
        Assert.Equal(2, first.Data.TotalPages);
        Assert.Equal(1, first.Data.PageSize);
        List<Guid> displayed = first.Data.Items.Select(item => item.ReceiptId).ToList();
        bool firstDocumentSelected = displayed.Contains(nearest.Id);
        if (firstDocumentSelected)
        {
            Assert.Contains(copy.Id, displayed);
            Assert.Equal(next.Id, Assert.Single(second.Data.Items).ReceiptId);
        }
        else
        {
            Assert.Equal(next.Id, Assert.Single(displayed));
            Assert.Equal(2, second.Data.Items.Count);
            Assert.Contains(nearest.Id, second.Data.Items.Select(item => item.ReceiptId));
            Assert.Contains(copy.Id, second.Data.Items.Select(item => item.ReceiptId));
        }
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2026, 1, 1)]
    public async Task MovementCandidates_OrderExactDateDistanceBeforeTakeAndClampPages(int year, int month, int day)
    {
        // Arrange
        Fixture data = new();
        DateTime anchor = new DateTime(year, month, day).AddTicks(1234567);
        Receipt receipt = TestReceiptFactory.Create(createdByUserId: data.UserId, dateTime: anchor);
        Assert.True(receipt.TryAddAccount(data.Account, out _, out _));
        data.Receipts.Add(receipt);
        MoneyMovement far = TestMoneyMovementFactory.CreateManual(accountId: data.Account.Id,
            occurredAt: anchor.AddDays(-1), userId: data.UserId);
        MoneyMovement nearest = TestMoneyMovementFactory.CreateManual(accountId: data.Account.Id,
            occurredAt: anchor.AddTicks(1), userId: data.UserId);
        MoneyMovement secondNearest = TestMoneyMovementFactory.CreateManual(accountId: data.Account.Id,
            occurredAt: anchor.AddTicks(-2), userId: data.UserId);
        data.Movements.AddRange([far, secondNearest, nearest]);
        GetReceiptMoneyMovementCandidatesUseCase useCase = TestUseCaseFactory.CreateGetReceiptMoneyMovementCandidatesUseCase(data.Work.Object);

        // Act
        var first = await useCase.ExecuteAsync(new() { ReceiptId = receipt.Id, PageSize = 1 },
            data.UserId, CancellationToken.None);
        var second = await useCase.ExecuteAsync(new() { ReceiptId = receipt.Id, PageSize = 1, Page = 2 },
            data.UserId, CancellationToken.None);
        var last = await useCase.ExecuteAsync(new() { ReceiptId = receipt.Id, PageSize = 1, Page = 999 },
            data.UserId, CancellationToken.None);

        // Assert
        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.True(last.Success);
        Assert.Equal(3, first.Data.TotalCount);
        Assert.Equal(nearest.Id, Assert.Single(first.Data.Items).MoneyMovementId);
        Assert.Equal(secondNearest.Id, Assert.Single(second.Data.Items).MoneyMovementId);
        Assert.Equal(far.Id, Assert.Single(last.Data.Items).MoneyMovementId);
        Assert.Equal(3, last.Data.Page);
        Assert.Equal(3, data.MovementReads);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("account")]
    [InlineData("hiddenAccount")]
    [InlineData("revoked")]
    public async Task CatalogPredicates_ExcludeUnmatchedPurchasesBeforeMaterialization(string excludedReason)
    {
        // Arrange
        Fixture data = new();
        Store matchedStore = data.AddStore("matched");
        Product matchedProduct = data.AddProduct("matched");
        data.AddPurchase(matchedStore, matchedProduct);
        Store excludedStore = data.AddStore("excluded");
        Product excludedProduct = data.AddProduct("excluded");
        Receipt excluded = data.AddPurchase(excludedStore, excludedProduct);
        Guid selectedAccountId = data.Account.Id;
        if (excludedReason == "before" || excludedReason == "after")
        {
            DateTime excludedDate = data.Date.AddDays(-1);
            if (excludedReason == "after")
            {
                excludedDate = data.Date.AddDays(1);
            }

            data.ReplacePurchase(excludedStore, excludedProduct, excludedDate, data.Account);
        }
        else if (excludedReason == "account")
        {
            data.ReplacePurchase(excludedStore, excludedProduct, data.Date,
                TestAccountFactory.Create(ownerUserId: data.UserId));
        }
        else if (excludedReason == "hiddenAccount")
        {
            Account hidden = TestAccountFactory.Create();
            Assert.True(excluded.TryAddAccount(hidden, out _, out _));
            selectedAccountId = hidden.Id;
        }
        else if (excludedReason == "revoked")
        {
            Account revoked = TestAccountFactory.Create();
            Assert.True(revoked.TryAddMember(data.UserId, AccountAccessRole.Viewer, out _, out _));
            data.ReplacePurchase(excludedStore, excludedProduct, data.Date, revoked, revoked.CreatedByUserId);
            Assert.True(revoked.TryRemoveMember(data.UserId, out _, out _));
        }

        // Act
        await TestUseCaseFactory.CreateGetProductListUseCase(data.Work.Object).ExecuteAsync(new()
        {
            DateFrom = data.Date, DateTo = data.Date, AccountId = selectedAccountId, PageSize = 1
        }, data.UserId, CancellationToken.None);
        await TestUseCaseFactory.CreateGetStoreListUseCase(data.Work.Object).ExecuteAsync(new()
        {
            DateFrom = data.Date, DateTo = data.Date, AccountId = selectedAccountId, PageSize = 1
        }, data.UserId, CancellationToken.None);

        // Assert
        Assert.NotEmpty(data.ProductRootReads);
        Assert.NotEmpty(data.StoreRootReads);
        Assert.All(data.ProductRootReads, ids => Assert.DoesNotContain(excludedProduct.Id, ids));
        Assert.All(data.StoreRootReads, ids => Assert.DoesNotContain(excludedStore.Id, ids));
        if (excludedReason != "hiddenAccount")
        {
            Assert.All(data.ProductRootReads, ids => Assert.Contains(matchedProduct.Id, ids));
            Assert.All(data.StoreRootReads, ids => Assert.Contains(matchedStore.Id, ids));
        }
    }

    [Theory]
    [InlineData("name")]
    [InlineData("receiptCount")]
    [InlineData("averagePrice")]
    public async Task Products_EqualSortValuesHaveStablePagesAndFullTotals(string sortBy)
    {
        // Arrange
        Fixture data = new();
        Store store = data.AddStore("Store");
        Product later = data.AddProduct("Product", Guid.Parse("00000000-0000-0000-0000-000000000002"));
        Product earlier = data.AddProduct("Product", Guid.Parse("00000000-0000-0000-0000-000000000001"));
        data.AddPurchase(store, later);
        data.AddPurchase(store, earlier);

        // Act
        var first = await TestUseCaseFactory.CreateGetProductListUseCase(data.Work.Object).ExecuteAsync(new()
        {
            Page = 1, PageSize = 1, SortBy = sortBy
        }, data.UserId, CancellationToken.None);
        var last = await TestUseCaseFactory.CreateGetProductListUseCase(data.Work.Object).ExecuteAsync(new()
        {
            Page = 99, PageSize = 1, SortBy = sortBy
        }, data.UserId, CancellationToken.None);

        // Assert
        Assert.True(first.Success);
        Assert.True(last.Success);
        Assert.Equal(earlier.Id, Assert.Single(first.Data.Items).Id);
        Assert.Equal(later.Id, Assert.Single(last.Data.Items).Id);
        Assert.Equal(2, last.Data.Page);
        Assert.Equal(2, first.Data.TotalCount);
        Assert.Equal(200m, first.Data.TotalSum);
        Assert.Equal(first.Data.TotalSum, last.Data.TotalSum);
    }

    [Fact]
    public async Task StoreReceipts_OnlyPageRowsRequestCandidateCountsAndCopiesStayTogether()
    {
        // Arrange
        Fixture data = new();
        Store store = data.AddStore("Store");
        Receipt first = data.AddPurchase(store, data.AddProduct("Product"), identity: "first");
        data.AddPurchase(store, data.AddProduct("Copy"), identity: "first", authorId: Guid.NewGuid());
        data.AddPurchase(store, data.AddProduct("Refund"), identity: "refund", type: ReceiptOperationType.RefundIncome);

        // Act
        var result = await TestUseCaseFactory.CreateGetStoreReceiptListUseCase(data.Work.Object).ExecuteAsync(new()
        {
            StoreId = store.Id, PageSize = 1
        }, data.UserId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data.Items);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(100m, result.Data.TotalSum);
        Assert.Equal(100m, result.Data.IncomeSum);
        Assert.Single(data.ReceiptReads.Last());
        Assert.Equal(result.Data.Items[0].Id, data.ReceiptReads.Last()[0]);
        Assert.Equal(3, data.Receipts.Count);
    }

    [Fact]
    public async Task EmptyStorePage_DoesNotRequestCandidates()
    {
        // Arrange
        Fixture data = new();
        Store store = data.AddStore("Empty");

        // Act
        var result = await TestUseCaseFactory.CreateGetStoreReceiptListUseCase(data.Work.Object).ExecuteAsync(new()
        {
            StoreId = store.Id, Page = 99, PageSize = 1
        }, data.UserId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Data.Items);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Empty(data.ReceiptReads);
        Assert.Equal(0, data.MovementReads);
    }

    private sealed class Fixture
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public DateTime Date { get; } = new(2026, 1, 15);
        public Account Account { get; }
        public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);
        public List<Product> Products { get; } = [];
        public List<Store> Stores { get; } = [];
        public List<Receipt> Receipts { get; } = [];
        public List<MoneyMovement> Movements { get; } = [];
        public List<List<Guid>> ReceiptProjectionReads { get; } = [];
        public List<Guid> MaterializedProducts { get; } = [];
        public List<Guid> MaterializedStores { get; } = [];
        public List<List<Guid>> ProductRootReads { get; } = [];
        public List<List<Guid>> StoreRootReads { get; } = [];
        public List<List<Guid>> ReceiptReads { get; } = [];
        public int MovementReads { get; private set; }

        public Fixture()
        {
            Account = TestAccountFactory.Create(ownerUserId: UserId);
            Mock<IProductRepository> products = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(products, () => Products,
                roots => ProductRootReads.Add(roots.Select(item => item.Id).ToList()));
            products.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Product, bool>> predicate, int skip, int? take, bool noTracking,
                    Func<IQueryable<Product>, IQueryable<Product>> include, CancellationToken ct) =>
                {
                    List<Product> result = Products.AsQueryable().Where(predicate).ToList();
                    MaterializedProducts.AddRange(result.Select(product => product.Id));
                    return result;
                });
            Mock<IStoreRepository> stores = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(stores, () => Stores,
                roots => StoreRootReads.Add(roots.Select(item => item.Id).ToList()));
            stores.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Store, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<Func<IQueryable<Store>, IQueryable<Store>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Store, bool>> predicate, int skip, int? take, bool noTracking,
                    Func<IQueryable<Store>, IQueryable<Store>> include, CancellationToken ct) =>
                {
                    List<Store> result = Stores.AsQueryable().Where(predicate).ToList();
                    MaterializedStores.AddRange(result.Select(store => store.Id));
                    return result;
                });
            Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(receipts, () => Receipts,
                roots => ReceiptProjectionReads.Add(roots.Select(item => item.Id).ToList()));
            receipts.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                true, It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, bool noTracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                    Receipts.AsQueryable().FirstOrDefault(predicate));
            receipts.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool noTracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                {
                    List<Receipt> result = Receipts.AsQueryable().Where(predicate).ToList();
                    ReceiptReads.Add(result.Select(receipt => receipt.Id).ToList());
                    return result;
                });
            Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(movements, () => Movements);
            movements.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true, It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<MoneyMovement, bool>> predicate, bool noTracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) =>
                    Movements.AsQueryable().FirstOrDefault(predicate));
            movements.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<MoneyMovement, bool>> predicate, int skip, int? take, bool noTracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) =>
                {
                    MovementReads++;
                    IQueryable<MoneyMovement> query = include(Movements.AsQueryable().Where(predicate)).Skip(skip);
                    if (take.HasValue)
                    {
                        query = query.Take(take.Value);
                    }
                    return query.ToList();
                });
            Work.SetupGet(work => work.Product).Returns(products.Object);
            Work.SetupGet(work => work.Store).Returns(stores.Object);
            Work.SetupGet(work => work.Receipt).Returns(receipts.Object);
            Work.SetupGet(work => work.MoneyMovement).Returns(movements.Object);
        }

        public Store AddStore(string name)
        {
            Store store = TestReceiptFactory.CreateStore(name);
            store.Id = Guid.NewGuid();
            Stores.Add(store);
            return store;
        }

        public Product AddProduct(string name, Guid? id = null)
        {
            Product product = TestReceiptFactory.CreateProduct(name);
            product.Id = id.GetValueOrDefault(Guid.NewGuid());
            Products.Add(product);
            return product;
        }

        public Receipt AddPurchase(Store store, Product product, string identity = "fp", Guid? authorId = null,
            ReceiptOperationType type = ReceiptOperationType.Income)
        {
            Receipt receipt = TestReceiptFactory.Create(createdByUserId: authorId ?? UserId, dateTime: Date,
                totalSum: 100m, operationType: type, store: store, fiscalSign: identity);
            Assert.True(receipt.TryAddAccount(Account, out _, out _));
            ReceiptItem item = TestReceiptFactory.CreateItem(product);
            Assert.True(receipt.TryAddItem(item, out _));
            TestReceiptFactory.AddMaterializedReceiptItem(product, item);
            MaterializedStoreReceipts(store).Add(receipt);
            Receipts.Add(receipt);
            return receipt;
        }

        public void ReplacePurchase(Store store, Product product, DateTime date, Account account, Guid? authorId = null)
        {
            Receipt receipt = TestReceiptFactory.Create(createdByUserId: authorId ?? UserId, dateTime: date, store: store);
            Assert.True(receipt.TryAddAccount(account, out _, out _));
            ReceiptItem item = TestReceiptFactory.CreateItem(product);
            Assert.True(receipt.TryAddItem(item, out _));
            FieldInfo field = typeof(Product).GetField("_receiptItems", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((List<ReceiptItem>)field.GetValue(product)!).Clear();
            TestReceiptFactory.AddMaterializedReceiptItem(product, item);
            MaterializedStoreReceipts(store).Clear();
            MaterializedStoreReceipts(store).Add(receipt);
        }

        private static List<Receipt> MaterializedStoreReceipts(Store store)
        {
            // Только тестовая имитация обратной навигации EF, не SQL-материализация.
            FieldInfo field = typeof(Store).GetField("_receipts", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (List<Receipt>)field.GetValue(store)!;
        }
    }
}
