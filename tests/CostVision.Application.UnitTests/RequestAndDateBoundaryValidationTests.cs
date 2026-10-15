using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Extensions;
using CostVision.Application.Models.Dtos.Dashboard;
using CostVision.Application.Models.Dtos;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.Models.Requests.Dashboard;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Dashboard;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Stores;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests;

public class RequestAndDateBoundaryValidationTests
{
    [Fact]
    public async Task TerminalDate_RejectsAllCalendarQueriesAndReconciliationBeforeStorage()
    {
        // Arrange
        Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        Mock<IProductRepository> products = new(MockBehavior.Strict);
        Mock<IStoreRepository> stores = new(MockBehavior.Strict);
        Mock<IReceiptReconciliationUnitOfWork> work = new(MockBehavior.Strict);
        User user = TestUserFactory.Create();
        DateTime end = DateTime.MaxValue;

        // Act
        List<ServiceError?> errors =
        [
            (await new GetReceiptListUseCase(receipts.Object).ExecuteAsync(user, end, end, CancellationToken.None)).Error,
            (await new GetReceiptListPageUseCase(receipts.Object, movements.Object).ExecuteAsync(user,
                new() { DateFrom = end, DateTo = end }, CancellationToken.None)).Error,
            (await new GetProductListUseCase(products.Object).ExecuteAsync(new() { DateTo = end }, user.Id, CancellationToken.None)).Error,
            (await new GetStoreListUseCase(stores.Object).ExecuteAsync(new() { DateTo = end }, user.Id, CancellationToken.None)).Error,
            (await new GetMoneyMovementListUseCase(movements.Object, receipts.Object).ExecuteAsync(user.Id, end, end, null, CancellationToken.None)).Error,
            (await new GetDashboardIncomeExpenseReportUseCase(receipts.Object, movements.Object).ExecuteAsync(user,
                new() { DateFrom = end, DateTo = end, Period = "day", ExpenseSource = "receipts" }, CancellationToken.None)).Error,
            (await new AutoLinkExactMoneyMovementReceiptsUseCase(work.Object).ExecuteAsync(user.Id, end, end, null, CancellationToken.None)).Error
        ];

        // Assert
        Assert.All(errors, error =>
        {
            Assert.NotNull(error);
            Assert.Equal(ServiceErrorType.Validation, error.Type);
            Assert.Equal("Дата окончания периода не допускает вычисления следующего дня.", error.Message);
        });
        receipts.VerifyNoOtherCalls();
        movements.VerifyNoOtherCalls();
        products.VerifyNoOtherCalls();
        stores.VerifyNoOtherCalls();
        work.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SameCalendarDayWithReversedTimes_IsAcceptedAndKeepsQueryPeriod()
    {
        // Arrange
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
        List<MoneyMovement> saved = [];
        ProjectedReadTestFactory.Configure(movements, () => saved);
        GetMoneyMovementListUseCase useCase = new(movements.Object, receipts.Object);
        DateTime day = new(2026, 10, 14);

        // Act
        ServiceResult<MoneyMovementListDto> result = await useCase.ExecuteAsync(
            Guid.NewGuid(), day.AddHours(23), day.AddHours(1), null, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Data!.Items);
        receipts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HugeTimeWindow_RejectsBeforeCandidateQueriesWithoutOverflow()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Receipt receipt = TestReceiptFactory.Create(createdByUserId: userId);
        Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        receipts.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<Receipt, bool>>>(),
                true, It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), CancellationToken.None))
            .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, bool tracking, Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                new[] { receipt }.AsQueryable().FirstOrDefault(predicate));
        GetReceiptMoneyMovementCandidatesUseCase useCase = new(receipts.Object, movements.Object);

        // Act
        ServiceResult<PagedListDto<ReceiptMoneyMovementDto>> result =
            await useCase.ExecuteAsync(new() { ReceiptId = receipt.Id, UseTimeWindow = true, TimeWindowHours = decimal.MaxValue }, userId, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error.Type);
        Assert.Contains("диапазон дат", result.Error.Message);
        movements.VerifyNoOtherCalls();
        receipts.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task MalformedWrites_RejectBeforeAccessTransactionOrSave(int scenario)
    {
        // Arrange
        Mock<IReceiptCreationUnitOfWork> receipts = new(MockBehavior.Strict);
        Mock<IValidateReceiptCreationAccessUseCase> access = new(MockBehavior.Strict);
        Mock<IQrParser> parser = new(MockBehavior.Strict);
        Mock<IBankStatementImportUnitOfWork> imports = new(MockBehavior.Strict);
        ServiceError? error;

        // Act
        if (scenario == 0)
        {
            error = (await new SaveReceiptsScannedUseCase(receipts.Object, access.Object, parser.Object)
                .ExecuteAsync(new() { Results = null! }, Guid.NewGuid(), CancellationToken.None)).Error;
        }
        else if (scenario == 1)
        {
            error = (await new SaveReceiptsScannedUseCase(receipts.Object, access.Object, parser.Object)
                .ExecuteAsync(new() { Results = [null!] }, Guid.NewGuid(), CancellationToken.None)).Error;
        }
        else if (scenario == 2)
        {
            error = (await new SaveManualReceiptUseCase(receipts.Object, access.Object)
                .ExecuteAsync(new() { Receipt = null! }, Guid.NewGuid(), CancellationToken.None)).Error;
        }
        else if (scenario == 3)
        {
            error = (await new ImportMoneyMovementsUseCase(imports.Object)
                .ExecuteAsync(new() { Rows = null! }, Guid.NewGuid(), CancellationToken.None)).Error;
        }
        else
        {
            error = (await new ImportMoneyMovementsUseCase(imports.Object)
                .ExecuteAsync(new() { Rows = [null!] }, Guid.NewGuid(), CancellationToken.None)).Error;
        }

        // Assert
        Assert.NotNull(error);
        Assert.Equal(ServiceErrorType.Validation, error.Type);
        receipts.VerifyNoOtherCalls();
        access.VerifyNoOtherCalls();
        parser.VerifyNoOtherCalls();
        imports.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LastSupportedMonth_DoesNotAdvancePastDateTimeRange()
    {
        // Arrange
        DateTime date = new(9999, 12, 30);
        Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        List<Receipt> saved = [];
        ProjectedReadTestFactory.Configure(receipts, () => saved);
        GetDashboardIncomeExpenseReportUseCase useCase = new(receipts.Object, movements.Object);
        User user = TestUserFactory.Create();

        // Act
        ServiceResult<DashboardIncomeExpenseReportDto> result = await useCase.ExecuteAsync(user,
            new() { DateFrom = date, DateTo = date, Period = "month", ExpenseSource = "receipts" }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        DashboardIncomeExpensePointDto point = Assert.Single(result.Data!.Points);
        Assert.Equal(new DateTime(9999, 12, 1), point.PeriodStart);
        Assert.Equal(0m, point.IncomeSum);
        Assert.Equal(0m, point.ExpenseSum);
    }

    [Fact]
    public void TimeWindow_PreservesZeroAndRejectsBothDateBoundaries()
    {
        // Arrange
        DateTime center = new(2026, 10, 14, 12, 0, 0);

        // Act
        ServiceResult zero = RequestDateValidation.TryTimeWindow(center, 0m, out DateTime from, out DateTime to);
        ServiceResult minimum = RequestDateValidation.TryTimeWindow(DateTime.MinValue, 1m, out _, out _);
        ServiceResult maximum = RequestDateValidation.TryTimeWindow(DateTime.MaxValue, 1m, out _, out _);

        // Assert
        Assert.True(zero.Success);
        Assert.Equal(center, from);
        Assert.Equal(center, to);
        Assert.False(minimum.Success);
        Assert.False(maximum.Success);
        Assert.Equal(ServiceErrorType.Validation, minimum.Error.Type);
        Assert.Equal(ServiceErrorType.Validation, maximum.Error.Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalExpenseDate_RejectsBeforeMutationOrSave(bool inferredType)
    {
        // Arrange
        Mock<IReceiptLinkUnitOfWork> work = new(MockBehavior.Strict);
        CreateMoneyMovementUseCase useCase = new(work.Object);
        CreateMoneyMovementRequest request = new()
        {
            AccountId = Guid.NewGuid(), Amount = 1m, Type = MoneyMovementType.Expense, OccurredAt = DateTime.MaxValue
        };
        if (inferredType)
        {
            request.Type = null;
            request.Amount = -1m;
        }

        // Act
        ServiceResult<MoneyMovementDto> result = await useCase.ExecuteAsync(request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error.Type);
        work.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReverseCandidateWindow_RejectsWithoutOverflowOrCandidateQuery()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(userId: userId);
        Mock<IMoneyMovementRepository> movements = new(MockBehavior.Strict);
        Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
        movements.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true, It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), CancellationToken.None))
            .ReturnsAsync((Expression<Func<MoneyMovement, bool>> predicate, bool tracking, Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>> include, CancellationToken ct) =>
                new[] { movement }.AsQueryable().FirstOrDefault(predicate));
        GetMoneyMovementReceiptCandidatesUseCase useCase = new(movements.Object, receipts.Object);

        // Act
        ServiceResult<PagedListDto<MoneyMovementReceiptDto>> result = await useCase.ExecuteAsync(
            new() { MoneyMovementId = movement.Id, UseTimeWindow = true, TimeWindowHours = decimal.MaxValue }, userId, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error.Type);
        Assert.Contains("диапазон дат", result.Error.Message);
        receipts.VerifyNoOtherCalls();
        movements.VerifyAll();
    }
}
