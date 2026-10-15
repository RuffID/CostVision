using CostVision.Tests;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Infrastructure.DataBase;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class FinancialReportQueryTranslationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetailsAndRefresh_TranslateActualAccessibleDocumentProjectionWithMovementGraph(bool refresh)
    {
        // Arrange
        using Fixture data = new();
        Mock<IReceiptAccessVerificationService> access = new(MockBehavior.Strict);
        access.Setup(service => service.UserHasAccessToReceipt(data.User, data.Receipt)).Returns(true);
        Mock<IReceiptRefreshWorkflow> workflow = new(MockBehavior.Strict);
        workflow.Setup(item => item.RefreshAsync(data.Receipt, It.IsAny<CancellationToken>(), It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(data.Receipt));

        // Act
        if (refresh)
        {
            var result = await TestUseCaseFactory.CreateRefreshReceiptFromApiUseCase(data.Work.Object, access.Object, workflow.Object)
                .ExecuteAsync(data.Receipt.Id, data.User, TestContext.Current.CancellationToken);
            Assert.True(result.Success);
        }
        else
        {
            var result = await TestUseCaseFactory.CreateGetReceiptWithItemsUseCase(data.Work.Object, access.Object)
                .ExecuteAsync(data.Receipt.Id, data.User, TestContext.Current.CancellationToken);
            Assert.True(result.Success);
        }

        // Assert
        string sql = Assert.Single(data.ReceiptListSql);
        foreach (string name in new[] { "FiscalDriveNumber", "FiscalDocumentNumber", "FiscalSign", "DateTime", "TotalSum", "OperationType",
            "CreatedByUserId", "UserId", "MoneyMovementId", "Amount", "AccountId" })
        {
            Assert.Contains("[" + name + "]", sql);
        }

        Assert.Contains("WHERE", sql);
        Assert.Contains("EXISTS", sql);
        Assert.DoesNotContain("Invoke", sql);
    }

    [Fact]
    public async Task DashboardAndCandidateCount_TranslateActualPredicatesAndIncludes()
    {
        // Arrange
        using Fixture data = new();

        // Act
        var dashboard = await TestUseCaseFactory.CreateGetDashboardIncomeExpenseReportUseCase(data.Work.Object).ExecuteAsync(data.User, new()
        {
            DateFrom = data.Date, DateTo = data.Date, Period = "day", ExpenseSource = "receiptsAndMoneyMovements",
            AccountIds = [Guid.NewGuid()], StoreNames = ["Store"]
        }, TestContext.Current.CancellationToken);
        var candidates = await TestUseCaseFactory.CreateGetReceiptMoneyMovementCandidatesUseCase(data.Work.Object).ExecuteAsync(new()
        {
            ReceiptId = data.Receipt.Id, DateFrom = data.Date.AddDays(-1), DateTo = data.Date.AddDays(1), AmountTolerance = 1m
        }, data.User.Id, TestContext.Current.CancellationToken);
        var list = await TestUseCaseFactory.CreateGetReceiptListPageUseCase(data.Work.Object)
            .ExecuteAsync(data.User, new() { DateFrom = data.Date, DateTo = data.Date }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(dashboard.Success);
        Assert.True(candidates.Success);
        Assert.True(list.Success);
        Assert.True(data.MovementSql.Count >= 3);
        foreach (string sql in data.MovementSql)
        {
            Assert.Contains("[OccurredAt]", sql);
            Assert.Contains("[UserId]", sql);
            Assert.Contains("[CreatedByUserId]", sql);
            Assert.Contains("WHERE", sql);
            Assert.DoesNotContain("Invoke", sql);
        }

        Assert.Contains(data.MovementSql, sql => sql.Contains("ABS(") && sql.Contains("NOT EXISTS"));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ApplicationContext context = new(new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=unused;Database=unused;User Id=unused;Password=unused;TrustServerCertificate=True").Options);
        public DateTime Date { get; } = new(2026, 1, 15);
        public User User { get; }
        public Receipt Receipt { get; }
        public List<string> ReceiptListSql { get; } = [];
        public List<string> MovementSql { get; } = [];
        public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);

        public Fixture()
        {
            Assert.True(User.TryCreate("user", "User", "hash", [Guid.NewGuid()], Date, out User? user, out _));
            User = user!;
            User.Id = Guid.NewGuid();
            Assert.True(Receipt.TryCreate("fn", "fd", "fp", Date, ReceiptOperationType.Income, 1000m, User.Id, Date,
                out Receipt? receipt, out _));
            Receipt = receipt!;
            Receipt.Id = Guid.NewGuid();
            Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
            ProjectedQueryCapture.Configure(receipts, context, () => new[] { Receipt }, ReceiptListSql);
            receipts.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                {
                    _ = include(context.Set<Receipt>().Where(predicate)).AsSingleQuery().ToQueryString();
                    return Task.FromResult(new[] { Receipt }.FirstOrDefault(predicate.Compile()));
                });
            receipts.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                {
                    ReceiptListSql.Add(include(context.Set<Receipt>().Where(predicate)).AsSingleQuery().ToQueryString());
                    return Task.FromResult(new[] { Receipt }.Where(predicate.Compile()).ToList());
                });
            Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
            ProjectedQueryCapture.Configure(movements, context, () => Array.Empty<MoneyMovement>(), MovementSql);
            movements.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<MoneyMovement, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) =>
                {
                    MovementSql.Add(include(context.Set<MoneyMovement>().Where(predicate)).AsSingleQuery().ToQueryString());
                    return Task.FromResult(new List<MoneyMovement>());
                });
            Work.Setup(work => work.Receipt).Returns(receipts.Object);
            Work.Setup(work => work.MoneyMovement).Returns(movements.Object);
        }

        public void Dispose()
        {
            context.Dispose();
        }
    }
}
