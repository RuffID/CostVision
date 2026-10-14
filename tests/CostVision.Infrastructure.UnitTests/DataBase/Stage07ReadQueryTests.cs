using CostVision.Tests;
using System.Linq.Expressions;
using System.Reflection;
using CostVision.Application.Abstractions.DataBase;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using EFCoreLibrary.Abstractions.Database.Repository.Base;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class Stage07ReadQueryTests
{
    [Theory]
    [InlineData("withOperations")]
    [InlineData("withoutOperations")]
    [InlineData("amountMismatch")]
    public async Task ActualReceiptFiltersAndOtherReadPaths_TranslateProjectedPagesWithoutConnection(string operationFilter)
    {
        // Arrange
        using ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True").Options);
        DateTime date = new(2026, 1, 15);
        Assert.True(User.TryCreate("user", "User", "hash", [Guid.NewGuid()], date, out User? user, out _));
        user!.Id = Guid.NewGuid();
        Assert.True(Account.TryCreate("Account", null, null, user.Id, date, out Account? account, out _));
        account!.Id = Guid.NewGuid();
        Assert.True(Receipt.TryCreate("fn", "fd", "fp", date.AddTicks(1234567), ReceiptOperationType.Income,
            100m, user.Id, date, out Receipt? receipt, out _));
        receipt!.Id = Guid.NewGuid();
        Assert.True(MoneyMovement.TryCreateManual(account, 100m, MoneyMovementType.Expense, date,
            "Comment", user, user, date, out MoneyMovement? movement, out _));
        movement!.Id = Guid.NewGuid();
        List<string> receiptSql = [];
        List<string> movementSql = [];
        List<string> itemSql = [];
        Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
        ProjectedQueryCapture.Configure(receipts, context, () => new[] { receipt }, receiptSql);
        receipts.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool noTracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
            {
                receiptSql.Add(include(context.Set<Receipt>().AsNoTracking().Where(predicate)).ToQueryString());
                return new[] { receipt }.AsQueryable().Where(predicate).ToList();
            });
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        ProjectedQueryCapture.Configure(movements, context, () => new[] { movement }, movementSql);
        movements.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true, It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<MoneyMovement, bool>> predicate, bool noTracking,
                Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) =>
                new[] { movement }.AsQueryable().FirstOrDefault(predicate));
        Mock<IReceiptItemRepository> items = new(MockBehavior.Strict);
        ProjectedQueryCapture.Configure(items, context, () => Array.Empty<ReceiptItem>(), itemSql);
        Mock<ITestStorage> work = new(MockBehavior.Strict);
        work.SetupGet(item => item.Receipt).Returns(receipts.Object);
        work.SetupGet(item => item.MoneyMovement).Returns(movements.Object);
        work.SetupGet(item => item.ReceiptItem).Returns(items.Object);

        // Act
        await TestUseCaseFactory.CreateGetReceiptListPageUseCase(work.Object).ExecuteAsync(user, new()
        {
            DateFrom = date, DateTo = date, Search = "fn", SearchMode = "fn", OperationFilter = operationFilter,
            PageSize = 1
        }, TestContext.Current.CancellationToken);
        await TestUseCaseFactory.CreateGetMoneyMovementListUseCase(work.Object).ExecuteAsync(user.Id, date, date, account.Id,
            TestContext.Current.CancellationToken, pageSize: 1, search: "Comment", receiptFilter: "amountMismatch");
        await TestUseCaseFactory.CreateGetMoneyMovementReceiptCandidatesUseCase(work.Object).ExecuteAsync(new()
        {
            MoneyMovementId = movement.Id, PageSize = 1, UseAmountFilter = true
        }, user.Id, TestContext.Current.CancellationToken);
        await TestUseCaseFactory.CreateGetProductStorePurchasesUseCase(work.Object).ExecuteAsync(new() { ProductId = Guid.NewGuid() },
            user.Id, TestContext.Current.CancellationToken);
        await TestUseCaseFactory.CreateGetDashboardIncomeExpenseReportUseCase(work.Object).ExecuteAsync(user, new()
        {
            DateFrom = date, DateTo = date, ExpenseSource = "receiptsAndMoneyMovements", Period = "day", StoreNames = ["Shop"]
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(receiptSql, sql => sql.Contains("OFFSET") && sql.Contains("NOT EXISTS"));
        Assert.Contains(receiptSql, sql => sql.Contains("DATEPART(nanosecond") && sql.Contains("ABS("));
        Assert.Contains(movementSql, sql => sql.Contains("FETCH NEXT") && sql.Contains("[Comment]") && sql.Contains("ABS("));
        Assert.Contains("GROUP BY", Assert.Single(itemSql));
        Assert.DoesNotContain("RowVersion", Assert.Single(itemSql));
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void SuppliedDll_ListContractPreservesEntityTypeAndDoesNotExposeAggregates()
    {
        // Arrange
        Type contract = typeof(IGetItemByPredicateRepository<Receipt>);

        // Act
        MethodInfo list = contract.GetMethod("GetItemsByPredicateAsync")!;
        ParameterInfo[] parameters = list.GetParameters();
        Type queryContract = contract.Assembly.GetType(
            "EFCoreLibrary.Abstractions.Database.Repository.Base.IContextQueryRepository`2", throwOnError: true)!
            .MakeGenericType(typeof(Receipt), typeof(CostVisionContextKey));

        // Assert
        Assert.Equal(typeof(Task<List<Receipt>>), list.ReturnType);
        Assert.False(list.IsGenericMethod);
        Assert.Equal(new[] { "predicate", "skip", "take", "asNoTracking", "include", "ct" },
            parameters.Select(parameter => parameter.Name));
        Assert.Equal(0, parameters[1].DefaultValue);
        Assert.Null(parameters[2].DefaultValue);
        Assert.Equal(false, parameters[3].DefaultValue);
        Assert.Equal(typeof(Func<IQueryable<Receipt>, IQueryable<Receipt>>), parameters[4].ParameterType);
        Assert.Equal(new[] { "GetItemByPredicateAsync", "GetItemsByPredicateAsync" },
            contract.GetMethods().Select(method => method.Name).OrderBy(name => name));
        Assert.Contains(typeof(IQueryRepository<Receipt>), queryContract.GetInterfaces());
        Assert.Equal(typeof(IQueryable<Receipt>), typeof(IQueryRepository<Receipt>).GetMethod("Query")!.ReturnType);
        Assert.False(queryContract.IsAssignableFrom(typeof(IReceiptRepository)));
    }

    [Fact]
    public async Task Catalogs_ActualPredicatesAndFilteredIncludesTranslateWithoutConnection()
    {
        // Arrange
        using ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True").Options);
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime date = new(2026, 1, 15);
        string productSql = string.Empty;
        string storeSql = string.Empty;
        Mock<IProductRepository> products = new(MockBehavior.Strict);
        List<string> productQueries = [];
        ProjectedQueryCapture.Configure(products, context, () => Array.Empty<Product>(), productQueries);
        products.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(),
            0, null, true, It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, int skip, int? take, bool noTracking,
                Func<IQueryable<Product>, IQueryable<Product>> include, CancellationToken ct) =>
            {
                productSql = include(context.Set<Product>().AsNoTracking().Where(predicate)).AsSingleQuery().ToQueryString();
                return [];
            });
        Mock<IStoreRepository> stores = new(MockBehavior.Strict);
        List<string> storeQueries = [];
        ProjectedQueryCapture.Configure(stores, context, () => Array.Empty<Store>(), storeQueries);
        stores.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Store, bool>>>(),
            0, null, true, It.IsAny<Func<IQueryable<Store>, IQueryable<Store>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Store, bool>> predicate, int skip, int? take, bool noTracking,
                Func<IQueryable<Store>, IQueryable<Store>> include, CancellationToken ct) =>
            {
                storeSql = include(context.Set<Store>().AsNoTracking().Where(predicate)).AsSingleQuery().ToQueryString();
                return [];
            });
        Mock<ITestStorage> work = new(MockBehavior.Strict);
        work.SetupGet(item => item.Product).Returns(products.Object);
        work.SetupGet(item => item.Store).Returns(stores.Object);

        // Act
        await TestUseCaseFactory.CreateGetProductListUseCase(work.Object).ExecuteAsync(new()
        {
            DateFrom = date, DateTo = date, AccountId = accountId
        }, userId, TestContext.Current.CancellationToken);
        await TestUseCaseFactory.CreateGetStoreListUseCase(work.Object).ExecuteAsync(new()
        {
            DateFrom = date, DateTo = date, AccountId = accountId
        }, userId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, productQueries.Count);
        Assert.Equal(3, storeQueries.Count);
        foreach (string sql in productQueries.Concat(storeQueries))
        {
            Assert.Contains("EXISTS", sql);
            Assert.Contains("[AccountMembers]", sql);
            Assert.Contains("[CreatedByUserId]", sql);
            Assert.Contains("[AccountId]", sql);
            Assert.Contains("[DateTime] >=", sql);
            Assert.Contains("[DateTime] <", sql);
        }

        Assert.True(storeQueries.Last().Split("[DateTime] >=", StringSplitOptions.None).Length >= 3);
        Assert.True(storeQueries.Last().Split("[DateTime] <", StringSplitOptions.None).Length >= 3);
        Assert.Contains("OFFSET", storeQueries.Last());
        Assert.Contains("FETCH NEXT", storeQueries.Last());

        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
        Assert.Empty(context.ChangeTracker.Entries());
    }
}
