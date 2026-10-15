using CostVision.Tests;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests;

public class FinancialReportTotalsTests
{
    [Theory]
    [InlineData("receipts")]
    [InlineData("receiptsAndMoneyMovements")]
    public async Task Dashboard_CountsDocumentOnceAcrossAuthors(string source)
    {
        // Arrange
        Fixture data = new();
        data.AddReceipt();
        data.AddReceipt(author: Guid.NewGuid());

        // Act
        var result = await data.Dashboard(source);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1000m, Assert.Single(result.Data.Points).ExpenseSum);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dashboard_ExcludedOrInaccessibleMovementDoesNotRemoveReceipt(bool hidden)
    {
        // Arrange
        Fixture data = new();
        Receipt receipt = data.AddReceipt();
        Account other = TestAccountFactory.Create(ownerUserId: hidden ? Guid.NewGuid() : data.User.Id);
        Assert.True(receipt.TryAddAccount(other, out _, out _));
        MoneyMovement movement = data.AddMovement(other, 1000m);
        TestMoneyMovementFactory.CreateLink(movement, receipt);

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements", [data.Account.Id]);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1000m, Assert.Single(result.Data.Points).ExpenseSum);
    }

    [Fact]
    public async Task Dashboard_HiddenAccountSelectionDoesNotSelectOwnReceipt()
    {
        // Arrange
        Fixture data = new();
        Receipt receipt = data.AddReceipt();
        Account hidden = TestAccountFactory.Create();
        Assert.True(receipt.TryAddAccount(hidden, out _, out _));

        // Act
        var result = await data.Dashboard("receipts", [hidden.Id]);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0m, Assert.Single(result.Data.Points).ExpenseSum);
        Assert.Empty(result.Data.Stores);
    }

    [Fact]
    public async Task DocumentAggregates_ListDetailsAndRefreshCountDistinctAccessibleMovements()
    {
        // Arrange
        Fixture data = new();
        Receipt first = data.AddReceipt();
        Receipt copy = data.AddReceipt(author: Guid.NewGuid());
        MoneyMovement movement = data.AddMovement(data.Account, 400m);
        TestMoneyMovementFactory.CreateLink(movement, first);
        TestMoneyMovementFactory.CreateLink(movement, copy);
        MoneyMovement second = data.AddMovement(data.Account, 600m);
        TestMoneyMovementFactory.CreateLink(second, copy);
        MoneyMovement hidden = data.AddMovement(TestAccountFactory.Create(), 9000m);
        TestMoneyMovementFactory.CreateLink(hidden, first);
        Mock<IReceiptAccessVerificationService> access = new(MockBehavior.Strict);
        access.Setup(service => service.UserHasAccessToReceipt(data.User, first)).Returns(true);
        Mock<IReceiptRefreshWorkflow> refresh = new(MockBehavior.Strict);
        refresh.Setup(workflow => workflow.RefreshAsync(first, It.IsAny<CancellationToken>(), It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(first));

        // Act
        var list = await TestUseCaseFactory.CreateGetReceiptListUseCase(data.Work.Object).ExecuteAsync(data.User, data.Date, data.Date, CancellationToken.None);
        var details = await TestUseCaseFactory.CreateGetReceiptWithItemsUseCase(data.Work.Object, access.Object).ExecuteAsync(first.Id, data.User, CancellationToken.None);
        var refreshed = await TestUseCaseFactory.CreateRefreshReceiptFromApiUseCase(data.Work.Object, access.Object, refresh.Object).ExecuteAsync(first.Id, data.User, CancellationToken.None);

        // Assert
        Assert.True(list.Success);
        Assert.True(details.Success);
        Assert.True(refreshed.Success);
        foreach (ReceiptDto dto in new[] { Assert.Single(list.Data), details.Data, refreshed.Data })
        {
            Assert.Equal(2, dto.MoneyMovementCount);
            Assert.Equal(1000m, dto.MoneyMovementsTotalSum);
        }

    }

    [Theory]
    [InlineData(AccountAccessRole.Owner)]
    [InlineData(AccountAccessRole.Editor)]
    [InlineData(AccountAccessRole.Viewer)]
    public async Task ReportCandidateCountsMatchInitialModalAcrossRoles(AccountAccessRole role)
    {
        // Arrange
        Fixture data = new(role);
        Receipt receipt = data.AddReceipt();
        data.AddMovement(data.Account, 1001m, data.Date.AddDays(-1));
        data.AddMovement(data.Account, 999m, data.Date.AddDays(1).AddHours(23), MoneyMovementType.Income);
        data.AddMovement(data.Account, 1002m);
        data.AddMovement(data.Account, 1000m, data.Date.AddDays(2));
        MoneyMovement occupied = data.AddMovement(data.Account, 1000m);
        TestMoneyMovementFactory.CreateLink(occupied, TestReceiptFactory.Create());

        // Act
        var candidates = await TestUseCaseFactory.CreateGetReceiptMoneyMovementCandidatesUseCase(data.Work.Object).ExecuteAsync(new()
        {
            ReceiptId = receipt.Id, DateFrom = data.Date.AddDays(-1), DateTo = data.Date.AddDays(1), AmountTolerance = 1m
        }, data.User.Id, CancellationToken.None);
        var list = await TestUseCaseFactory.CreateGetReceiptListPageUseCase(data.Work.Object)
            .ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date, PageSize = 10 }, CancellationToken.None);
        var store = await TestUseCaseFactory.CreateGetStoreReceiptListUseCase(data.Work.Object)
            .ExecuteAsync(new() { StoreId = data.Store.Id, PageSize = 10 }, data.User.Id, CancellationToken.None);

        // Assert
        Assert.True(candidates.Success);
        Assert.Equal(2, candidates.Data.Items.Count);
        Assert.True(list.Success);
        Assert.True(store.Success);
        Assert.Equal(2, Assert.Single(list.Data.Items).AvailableMoneyMovementCount);
        Assert.Equal(2, Assert.Single(store.Data.Items).AvailableMoneyMovementCount);
    }

    [Theory]
    [InlineData(ReceiptOperationType.Income, 1000, 0)]
    [InlineData(ReceiptOperationType.RefundIncome, 0, 1000)]
    [InlineData(ReceiptOperationType.Expense, 0, 1000)]
    [InlineData(ReceiptOperationType.RefundExpense, 1000, 0)]
    public async Task Dashboard_ReceiptDirectionUsesCustomerPerspective(ReceiptOperationType type, int expense, int income)
    {
        // Arrange
        Fixture data = new();
        data.AddReceipt(type: type);
        data.AddMovement(data.Account, 5000m, type: MoneyMovementType.Income);

        // Act
        var result = await data.Dashboard("receipts");

        // Assert
        Assert.True(result.Success);
        var point = Assert.Single(result.Data.Points);
        Assert.Equal((decimal)expense, point.ExpenseSum);
        Assert.Equal((decimal)income, point.IncomeSum);
    }

    [Theory]
    [InlineData(400, 0, 1000)]
    [InlineData(400, 600, 1000)]
    [InlineData(700, 700, 1400)]
    public async Task Dashboard_PartialFullAndExcessCoverage(int firstAmount, int secondAmount, int expected)
    {
        // Arrange
        Fixture data = new();
        Receipt first = data.AddReceipt();
        Receipt copy = data.AddReceipt(author: Guid.NewGuid());
        MoneyMovement firstPayment = data.AddMovement(data.Account, firstAmount);
        TestMoneyMovementFactory.CreateLink(firstPayment, first);
        TestMoneyMovementFactory.CreateLink(firstPayment, copy);
        if (secondAmount > 0)
        {
            TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, secondAmount), copy);
        }

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements");

        // Assert
        Assert.True(result.Success);
        Assert.Equal((decimal)expected, Assert.Single(result.Data.Points).ExpenseSum);
    }

    [Fact]
    public async Task Dashboard_OnePaymentDoesNotCoverTwoDocumentsTwice()
    {
        // Arrange
        Fixture data = new();
        Receipt first = data.AddReceipt();
        Receipt second = data.AddReceipt(identity: "different");
        MoneyMovement payment = data.AddMovement(data.Account, 1000m);
        TestMoneyMovementFactory.CreateLink(payment, first);
        TestMoneyMovementFactory.CreateLink(payment, second);

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2000m, Assert.Single(result.Data.Points).ExpenseSum);
    }

    [Theory]
    [InlineData("receipts", 1000, 300)]
    [InlineData("moneyMovements", 0, 300)]
    [InlineData("receiptsAndMoneyMovements", 1000, 300)]
    public async Task Dashboard_RefundIsSeparateIncomeAndNotCountedTwice(string source, int expense, int income)
    {
        // Arrange
        Fixture data = new();
        data.AddReceipt();
        Receipt refund = data.AddReceipt(identity: "refund", type: ReceiptOperationType.RefundIncome, sum: 300m);
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 300m, type: MoneyMovementType.Income), refund);

        // Act
        var result = await data.Dashboard(source);

        // Assert
        Assert.True(result.Success);
        var point = Assert.Single(result.Data.Points);
        Assert.Equal((decimal)expense, point.ExpenseSum);
        Assert.Equal((decimal)income, point.IncomeSum);
    }

    [Fact]
    public async Task Dashboard_DifferentEqualPaymentsAreNotDeduplicated()
    {
        // Arrange
        Fixture data = new();
        data.AddMovement(data.Account, 700m);
        data.AddMovement(data.Account, 700m);
        data.AddReceipt();

        // Act
        var result = await data.Dashboard("moneyMovements");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1400m, Assert.Single(result.Data.Points).ExpenseSum);
    }

    [Theory]
    [InlineData("receipts", 0)]
    [InlineData("moneyMovements", 5000)]
    [InlineData("receiptsAndMoneyMovements", 5000)]
    public async Task Dashboard_OnlyRequestedSourcesContributeIncome(string source, int expected)
    {
        // Arrange
        Fixture data = new();
        data.AddMovement(data.Account, 5000m, type: MoneyMovementType.Income);

        // Act
        var result = await data.Dashboard(source);

        // Assert
        Assert.True(result.Success);
        var point = Assert.Single(result.Data.Points);
        Assert.Equal((decimal)expected, point.IncomeSum);
        Assert.Equal(0m, point.ExpenseSum);
    }

    [Theory]
    [InlineData("receipts")]
    [InlineData("moneyMovements")]
    [InlineData("receiptsAndMoneyMovements")]
    public async Task Dashboard_EmptyPeriodsHaveExplicitZeroPoints(string source)
    {
        // Arrange
        Fixture data = new();

        // Act
        var result = await data.Dashboard(source, from: data.Date, to: data.Date.AddDays(2));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.Data.Points.Count);
        Assert.All(result.Data.Points, point =>
        {
            Assert.Equal(0m, point.IncomeSum);
            Assert.Equal(0m, point.ExpenseSum);
        });
    }

    [Theory]
    [InlineData("day", 4)]
    [InlineData("month", 2)]
    public async Task Dashboard_UsesInclusiveCalendarDatesAndCorrectPeriodBuckets(string period, int pointCount)
    {
        // Arrange
        Fixture data = new();
        DateTime start = new(2026, 1, 30);
        DateTime end = new(2026, 2, 2);
        data.AddReceipt(identity: "before", date: start.AddTicks(-1));
        data.AddReceipt(identity: "start", date: start, sum: 100m);
        data.AddReceipt(identity: "january", date: new DateTime(2026, 1, 31, 23, 59, 59), sum: 200m);
        data.AddReceipt(identity: "february", date: new DateTime(2026, 2, 1), sum: 300m);
        data.AddReceipt(identity: "end", date: end.AddDays(1).AddTicks(-1), sum: 400m);
        data.AddReceipt(identity: "after", date: end.AddDays(1));

        // Act
        var result = await data.Dashboard("receipts", from: start.AddHours(18), to: end.AddHours(9), period: period);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(pointCount, result.Data.Points.Count);
        if (period == "month")
        {
            Assert.Equal(new[] { 300m, 700m }, result.Data.Points.Select(point => point.ExpenseSum));
            Assert.Equal(new DateTime(2026, 1, 1), result.Data.Points[0].PeriodStart);
            Assert.Equal(new DateTime(2026, 2, 1), result.Data.Points[1].PeriodStart);
        }
        else
        {
            Assert.Equal(new[] { 100m, 200m, 300m, 400m }, result.Data.Points.Select(point => point.ExpenseSum));
        }
    }

    [Fact]
    public async Task Dashboard_PartialCoverageUsesPaymentDateAndReceiptRemainderDate()
    {
        // Arrange
        Fixture data = new();
        Receipt receipt = data.AddReceipt();
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 400m, data.Date.AddDays(1)), receipt);

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements", to: data.Date.AddDays(1));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(new[] { 600m, 400m }, result.Data.Points.Select(point => point.ExpenseSum));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dashboard_SharedPaymentCoversEarlierDocumentRegardlessOfInputOrder(bool reverse)
    {
        // Arrange
        Fixture data = new();
        Receipt early = data.AddReceipt();
        Receipt late = data.AddReceipt(identity: "later", date: data.Date.AddDays(1));
        MoneyMovement payment = data.AddMovement(data.Account, 1000m, data.Date.AddDays(1));
        TestMoneyMovementFactory.CreateLink(payment, early);
        TestMoneyMovementFactory.CreateLink(payment, late);
        if (reverse)
        {
            data.Receipts.Reverse();
        }

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements", to: data.Date.AddDays(1));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(new[] { 0m, 2000m }, result.Data.Points.Select(point => point.ExpenseSum));
    }

    [Fact]
    public async Task Dashboard_ReallocatesSharedCoverageToAvoidFalseUnpaidRemainder()
    {
        // Arrange
        Fixture data = new();
        Receipt early = data.AddReceipt();
        Receipt late = data.AddReceipt(identity: "late", date: data.Date.AddDays(1));
        MoneyMovement shared = data.AddMovement(data.Account, 1000m);
        MoneyMovement earlyOnly = data.AddMovement(data.Account, 1000m, data.Date.AddHours(1));
        TestMoneyMovementFactory.CreateLink(shared, early);
        TestMoneyMovementFactory.CreateLink(shared, late);
        TestMoneyMovementFactory.CreateLink(earlyOnly, early);

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements", to: data.Date.AddDays(1));

        // Assert
        Assert.True(result.Success);
        Assert.Equal(new[] { 2000m, 0m }, result.Data.Points.Select(point => point.ExpenseSum));
        Assert.Equal(2, shared.ReceiptLinks.Count);
        Assert.Single(earlyOnly.ReceiptLinks);
    }

    [Theory]
    [InlineData("receipts", 1000)]
    [InlineData("moneyMovements", 1000)]
    [InlineData("receiptsAndMoneyMovements", 1000)]
    public async Task Dashboard_StoreFilterUsesVisibleReceiptStoresAndKeepsBankIncome(string source, int expectedExpense)
    {
        // Arrange
        Fixture data = new();
        Receipt receipt = data.AddReceipt();
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 1000m), receipt);
        Receipt otherStore = data.AddReceipt(identity: "other-store");
        otherStore.AssignStore(TestReceiptFactory.CreateStore("Other"));
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 2000m), otherStore);
        data.AddMovement(data.Account, 5000m, type: MoneyMovementType.Income);

        // Act
        var result = await data.Dashboard(source, stores: [" store "]);

        // Assert
        Assert.True(result.Success);
        var point = Assert.Single(result.Data.Points);
        Assert.Equal((decimal)expectedExpense, point.ExpenseSum);
        if (source == "receipts")
        {
            Assert.Equal(0m, point.IncomeSum);
        }
        else
        {
            Assert.Equal(5000m, point.IncomeSum);
        }
    }

    [Fact]
    public async Task Dashboard_LinkedHiddenReceiptCannotSupplyStoreFilterOrCatalog()
    {
        // Arrange
        Fixture data = new();
        Receipt hidden = TestReceiptFactory.Create(store: TestReceiptFactory.CreateStore("Secret"), dateTime: data.Date);
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 1000m), hidden);
        data.Receipts.Add(hidden);

        // Act
        var result = await data.Dashboard("moneyMovements", stores: ["Secret"]);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Data.Stores);
        Assert.Equal(0m, Assert.Single(result.Data.Points).ExpenseSum);
    }

    [Fact]
    public async Task Dashboard_WrongDirectionAndOutOfPeriodLinksDoNotCoverPurchase()
    {
        // Arrange
        Fixture data = new();
        Receipt receipt = data.AddReceipt();
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 1000m, type: MoneyMovementType.Income), receipt);
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 1000m, data.Date.AddDays(-1)), receipt);

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements");

        // Assert
        Assert.True(result.Success);
        var point = Assert.Single(result.Data.Points);
        Assert.Equal(1000m, point.ExpenseSum);
        Assert.Equal(1000m, point.IncomeSum);
    }

    [Fact]
    public async Task Refresh_DeniedAccessCannotInvokeWorkflowOrReturnHiddenAggregates()
    {
        // Arrange
        Fixture data = new(AccountAccessRole.Viewer);
        Receipt receipt = data.AddReceipt(author: data.Account.CreatedByUserId);
        data.AddMovement(data.Account, 9999m);
        Assert.True(data.Account.TryRemoveMember(data.User.Id, out _, out _));
        Mock<IReceiptAccessVerificationService> permissive = new(MockBehavior.Strict);
        permissive.Setup(service => service.UserHasAccessToReceipt(data.User, receipt)).Returns(true);
        Mock<IReceiptRefreshWorkflow> workflow = new(MockBehavior.Strict);

        // Act
        var result = await TestUseCaseFactory.CreateRefreshReceiptFromApiUseCase(data.Work.Object, permissive.Object, workflow.Object)
            .ExecuteAsync(receipt.Id, data.User, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error.Type);
        Assert.Null(result.Data);
        workflow.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(QuantityMeasureType.Gram, 500, 0.5, true)]
    [InlineData(QuantityMeasureType.Kilogram, 2, 2, true)]
    [InlineData(QuantityMeasureType.Ton, 1, 1000, true)]
    [InlineData(QuantityMeasureType.Piece, 3, 3, false)]
    [InlineData(QuantityMeasureType.Liter, 4, 4, false)]
    public async Task ProductReports_UseSameApprovedUnits(QuantityMeasureType measure, int quantity, double expectedQuantity, bool weighted)
    {
        // Arrange
        Fixture data = new();
        Product product = TestReceiptFactory.CreateProduct("Product");
        product.Id = Guid.NewGuid();
        Receipt receipt = data.AddReceipt();
        Assert.True(ReceiptItem.TryCreate(100m, quantity, 200m, 0, PaymentType.FullPayment, ProductType.Product,
            measure, product, null, out ReceiptItem? item, out _));
        item!.Id = Guid.NewGuid();
        Assert.True(receipt.TryAddItem(item, out _));
        TestReceiptFactory.AddMaterializedReceiptItem(product, item);
        Mock<IProductRepository> products = new(MockBehavior.Strict);
        products.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(),
            It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Product, bool>> predicate, int skip, int? take, bool tracking,
                Func<IQueryable<Product>, IQueryable<Product>> include, CancellationToken ct) =>
                Task.FromResult(new[] { product }.Where(predicate.Compile()).ToList()));
        Mock<IReceiptItemRepository> items = new(MockBehavior.Strict);
        ProjectedReadTestFactory.Configure(items, () => new[] { item! });
        items.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<ReceiptItem, bool>>>(),
            It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<ReceiptItem, bool>> predicate, int skip, int? take, bool tracking,
                Func<IQueryable<ReceiptItem>, IQueryable<ReceiptItem>> include, CancellationToken ct) =>
                Task.FromResult(new[] { item }.Where(predicate.Compile()).ToList()));
        data.Work.Setup(work => work.Product).Returns(products.Object);
        ProjectedReadTestFactory.Configure(products, () => new[] { product });
        data.Work.Setup(work => work.ReceiptItem).Returns(items.Object);

        // Act
        var list = await TestUseCaseFactory.CreateGetProductListUseCase(data.Work.Object).ExecuteAsync(new(), data.User.Id, CancellationToken.None);
        var purchases = await TestUseCaseFactory.CreateGetProductStorePurchasesUseCase(data.Work.Object)
            .ExecuteAsync(new() { ProductId = product.Id }, data.User.Id, CancellationToken.None);

        // Assert
        Assert.True(list.Success);
        Assert.True(purchases.Success);
        var purchase = Assert.Single(purchases.Data);
        var listed = Assert.Single(list.Data.Items);
        Assert.Equal((decimal)expectedQuantity, purchase.Quantity);
        Assert.Equal(200m / (decimal)expectedQuantity, purchase.PricePerUnit);
        Assert.Equal(purchase.PricePerUnit, listed.AveragePrice);
        Assert.Equal(weighted, purchase.IsWeighted);
        Assert.Equal(weighted, listed.AveragePriceIsWeighted);
    }

    [Fact]
    public async Task Dashboard_WithoutAccountAndRevokedMembershipAreNotInferredFromHiddenLinks()
    {
        // Arrange
        Fixture data = new(AccountAccessRole.Viewer);
        Receipt own = TestReceiptFactory.Create(createdByUserId: data.User.Id, dateTime: data.Date, totalSum: 250m);
        data.Receipts.Add(own);
        data.AddReceipt(author: data.Account.CreatedByUserId);
        data.AddMovement(data.Account, 9000m);
        Assert.True(data.Account.TryRemoveMember(data.User.Id, out _, out _));

        // Act
        var result = await data.Dashboard("receiptsAndMoneyMovements", withoutAccount: true);

        // Assert
        Assert.True(result.Success);
        var point = Assert.Single(result.Data.Points);
        Assert.Equal(250m, point.ExpenseSum);
        Assert.Equal(0m, point.IncomeSum);
    }

    [Fact]
    public async Task DocumentAggregates_EmptyProjectionFailsInsteadOfReturningUnknownZero()
    {
        // Arrange
        Fixture data = new();
        Receipt receipt = data.AddReceipt();
        TestMoneyMovementFactory.CreateLink(data.AddMovement(data.Account, 400m), receipt);
        Mock<IReceiptAccessVerificationService> access = new(MockBehavior.Strict);
        access.Setup(service => service.UserHasAccessToReceipt(data.User, receipt)).Returns(true);
        Mock<IReceiptRefreshWorkflow> refresh = new(MockBehavior.Strict);
        refresh.Setup(workflow => workflow.RefreshAsync(receipt, It.IsAny<CancellationToken>(), It.IsAny<Guid>()))
            .Returns(() =>
            {
                data.Receipts.Clear();
                return Task.FromResult(ServiceResult<Receipt>.Ok(receipt));
            });

        // Act
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestUseCaseFactory.CreateRefreshReceiptFromApiUseCase(data.Work.Object, access.Object, refresh.Object)
                .ExecuteAsync(receipt.Id, data.User, CancellationToken.None));

        // Assert
        Assert.Contains("не загружен", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiptAndStoreReports_SeparateDirectionsAndDeduplicateDocumentsBeforePaging(bool groupStores)
    {
        // Arrange
        Fixture data = new();
        data.AddReceipt();
        data.AddReceipt(author: Guid.NewGuid());
        data.AddReceipt(identity: "return", type: ReceiptOperationType.RefundIncome, sum: 300m);
        data.AddReceipt(identity: "payout", type: ReceiptOperationType.Expense, sum: 50m);
        data.AddReceipt(identity: "payout-return", type: ReceiptOperationType.RefundExpense, sum: 70m);
        data.SetupStores(data.Store);

        // Act
        var list = await TestUseCaseFactory.CreateGetReceiptListPageUseCase(data.Work.Object)
            .ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date, PageSize = 1 }, CancellationToken.None);
        var storeReceipts = await TestUseCaseFactory.CreateGetStoreReceiptListUseCase(data.Work.Object)
            .ExecuteAsync(new() { StoreId = data.Store.Id, PageSize = 1 }, data.User.Id, CancellationToken.None);
        var stores = await TestUseCaseFactory.CreateGetStoreListUseCase(data.Work.Object)
            .ExecuteAsync(new() { GroupByName = groupStores }, data.User.Id, CancellationToken.None);
        var dashboard = await data.Dashboard("receipts");

        // Assert
        Assert.True(list.Success);
        Assert.True(storeReceipts.Success);
        Assert.True(stores.Success);
        Assert.True(dashboard.Success);
        Assert.Single(list.Data.Items);
        Assert.Single(storeReceipts.Data.Items);
        Assert.Equal(4, list.Data.TotalCount);
        Assert.Equal(4, storeReceipts.Data.TotalCount);
        var store = Assert.Single(stores.Data.Items);
        Assert.Equal(4, store.ReceiptCount);
        Assert.Equal(1070m, list.Data.TotalSum);
        Assert.Equal(350m, list.Data.IncomeSum);
        Assert.Equal(1070m, storeReceipts.Data.TotalSum);
        Assert.Equal(350m, storeReceipts.Data.IncomeSum);
        Assert.Equal(1070m, store.TotalSpent);
        Assert.Equal(350m, store.IncomeSum);
        Assert.Equal(1070m, stores.Data.TotalSum);
        Assert.Equal(350m, stores.Data.IncomeSum);
        var point = Assert.Single(dashboard.Data.Points);
        Assert.Equal(1070m, point.ExpenseSum);
        Assert.Equal(350m, point.IncomeSum);
    }

    [Fact]
    public async Task Stores_GroupTotalsDoNotCountDocumentAgainAcrossAddresses()
    {
        // Arrange
        Fixture data = new();
        data.AddReceipt();
        Assert.True(Store.TryCreate("Store", "STORE", "Other address", "OTHER ADDRESS", out Store? otherStore, out _));
        otherStore!.Id = Guid.NewGuid();
        data.AddReceipt(author: Guid.NewGuid()).AssignStore(otherStore);
        data.SetupStores(data.Store, otherStore);

        // Act
        var result = await TestUseCaseFactory.CreateGetStoreListUseCase(data.Work.Object)
            .ExecuteAsync(new() { GroupByName = true }, data.User.Id, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var group = Assert.Single(result.Data.Items);
        Assert.Equal(2, group.Children.Count);
        Assert.Equal(1, group.ReceiptCount);
        Assert.Equal(1000m, group.TotalSpent);
        Assert.Equal(1000m, result.Data.TotalSum);
    }

    [Fact]
    public async Task Stores_HiddenAccountFilterCannotRevealOwnReceiptPlacementOrAmounts()
    {
        // Arrange
        Fixture data = new();
        Receipt receipt = data.AddReceipt();
        Account hidden = TestAccountFactory.Create();
        Assert.True(receipt.TryAddAccount(hidden, out _, out _));
        data.SetupStores(data.Store);

        // Act
        var result = await TestUseCaseFactory.CreateGetStoreListUseCase(data.Work.Object)
            .ExecuteAsync(new() { AccountId = hidden.Id }, data.User.Id, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0m, result.Data.TotalSum);
        Assert.Equal(0m, result.Data.IncomeSum);
    }

    private sealed class Fixture
    {
        public User User { get; } = TestUserFactory.Create();
        public DateTime Date { get; } = new(2026, 1, 15);
        public Store Store { get; } = TestReceiptFactory.CreateStore("Store");
        public Account Account { get; }
        public List<Receipt> Receipts { get; } = [];
        public List<MoneyMovement> Movements { get; } = [];
        public List<string> ReceiptIncludes { get; } = [];
        public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);

        public Fixture(AccountAccessRole role = AccountAccessRole.Owner)
        {
            Store.Id = Guid.NewGuid();
            Account = TestAccountFactory.Create(ownerUserId: role == AccountAccessRole.Owner ? User.Id : Guid.NewGuid());
            if (role != AccountAccessRole.Owner)
            {
                Assert.True(Account.TryAddMember(User.Id, role, out _, out _));
            }

            Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(receipts, () => Receipts);
            receipts.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                {
                    CaptureInclude(include);
                    return Task.FromResult(Receipts.Where(predicate.Compile()).Skip(skip).Take(take ?? int.MaxValue).ToList());
                });
            receipts.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                {
                    CaptureInclude(include);
                    return Task.FromResult(Receipts.FirstOrDefault(predicate.Compile()));
                });
            Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(movements, () => Movements);
            movements.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovement, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) =>
                    Task.FromResult(Movements.Where(predicate.Compile()).Skip(skip).Take(take ?? int.MaxValue).ToList()));
            Work.Setup(work => work.Receipt).Returns(receipts.Object);
            Work.Setup(work => work.MoneyMovement).Returns(movements.Object);
        }

        private void CaptureInclude(Func<IQueryable<Receipt>, IQueryable<Receipt>> include)
        {
            if (include != null)
            {
                ReceiptIncludes.Add(include(Receipts.AsQueryable()).Expression.ToString());
            }
        }

        public void SetupStores(params Store[] stores)
        {
            // Имитация обратной навигации, которую EF заполняет при материализации Include(Store.Receipts).
            var field = typeof(Store).GetField("_receipts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            foreach (Store store in stores)
            {
                var materialized = (List<Receipt>)field.GetValue(store)!;
                materialized.Clear();
                materialized.AddRange(Receipts.Where(receipt => receipt.StoreId == store.Id));
            }

            Mock<IStoreRepository> repository = new(MockBehavior.Strict);
            ProjectedReadTestFactory.Configure(repository, () => stores);
            repository.Setup(item => item.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Store, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), true, It.IsAny<Func<IQueryable<Store>, IQueryable<Store>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Store, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Store>, IQueryable<Store>> include, CancellationToken ct) =>
                    Task.FromResult(stores.Where(predicate.Compile()).ToList()));
            Work.Setup(work => work.Store).Returns(repository.Object);
        }

        public Receipt AddReceipt(Guid? author = null, string identity = "fp",
            ReceiptOperationType type = ReceiptOperationType.Income, decimal sum = 1000m, DateTime? date = null)
        {
            Receipt receipt = TestReceiptFactory.Create(createdByUserId: author ?? User.Id, dateTime: date ?? Date,
                totalSum: sum, operationType: type, store: Store, fiscalSign: identity);
            Assert.True(receipt.TryAddAccount(Account, out _, out _));
            Receipts.Add(receipt);
            return receipt;
        }

        public MoneyMovement AddMovement(Account account, decimal amount, DateTime? date = null, MoneyMovementType type = MoneyMovementType.Expense)
        {
            MoneyMovement movement = TestMoneyMovementFactory.CreateManual(userId: account.CreatedByUserId,
                amount: amount, occurredAt: date ?? Date, type: type);
            Assert.True(movement.TryMoveToAccount(account, out _));
            Movements.Add(movement);
            return movement;
        }

        public Task<ServiceResult<CostVision.Application.Models.Dtos.Dashboard.DashboardIncomeExpenseReportDto>> Dashboard(
            string source, List<Guid>? accounts = null, DateTime? from = null, DateTime? to = null,
            string period = "day", List<string>? stores = null, bool withoutAccount = false)
        {
            return TestUseCaseFactory.CreateGetDashboardIncomeExpenseReportUseCase(Work.Object).ExecuteAsync(User, new DashboardIncomeExpenseReportRequest
            {
                DateFrom = from ?? Date, DateTo = to ?? Date, Period = period, ExpenseSource = source,
                AccountIds = accounts ?? [], StoreNames = stores ?? [], IncludeWithoutAccount = withoutAccount
            }, CancellationToken.None);
        }
    }
}
