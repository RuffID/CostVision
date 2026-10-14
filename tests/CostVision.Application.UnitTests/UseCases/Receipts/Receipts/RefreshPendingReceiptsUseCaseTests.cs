using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class RefreshPendingReceiptsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_RefreshesOnlyPendingDueReceiptsWithoutItems()
    {
        // Arrange
        DateTime attemptedAtUtc = new(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        Receipt dueReceipt = TestReceiptFactory.Create(Guid.NewGuid());
        Receipt completedReceipt = TestReceiptFactory.Create(Guid.NewGuid());
        Assert.True(completedReceipt.TryCompleteRefresh(attemptedAtUtc.AddDays(-1), registerAttempt: true, out string? completionError), completionError);

        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository([dueReceipt, completedReceipt]);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow
            .Setup(workflow => workflow.RefreshScheduledAsync(
                dueReceipt,
                attemptedAtUtc,
                attemptedAtUtc.AddDays(1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(dueReceipt));
        RefreshPendingReceiptsUseCase useCase = new(
            CreateScopeFactory(receiptRepository, refreshWorkflow),
            new Mock<ILogger<RefreshPendingReceiptsUseCase>>().Object,
            CreateTimeProvider(attemptedAtUtc).Object);

        // Act
        await useCase.ExecuteAsync(CancellationToken.None);

        // Assert
        refreshWorkflow.Verify(workflow => workflow.RefreshScheduledAsync(
            dueReceipt,
            attemptedAtUtc,
            attemptedAtUtc.AddDays(1),
            It.IsAny<CancellationToken>()), Times.Once);
        refreshWorkflow.Verify(workflow => workflow.RefreshScheduledAsync(
            completedReceipt,
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_LogsFailedScheduledAttempt()
    {
        // Arrange
        DateTime attemptedAtUtc = new(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc);
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid());
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository([receipt]);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow
            .Setup(workflow => workflow.RefreshScheduledAsync(
                receipt,
                attemptedAtUtc,
                attemptedAtUtc.AddDays(1),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(receipt.TryRegisterRefreshFailure(
                "External API error",
                attemptedAtUtc,
                attemptedAtUtc.AddDays(1),
                ReceiptRefreshPolicy.MAX_ATTEMPTS,
                out _)))
            .ReturnsAsync(ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "External API error"));
        Mock<ILogger<RefreshPendingReceiptsUseCase>> logger = new();
        RefreshPendingReceiptsUseCase useCase = new(
            CreateScopeFactory(receiptRepository, refreshWorkflow),
            logger.Object,
            CreateTimeProvider(attemptedAtUtc).Object);

        // Act
        await useCase.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, receipt.RefreshAttemptCount);
        logger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(receipt.Id.ToString())),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static Mock<IReceiptRepository> CreateReceiptRepository(List<Receipt> receipts)
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                Expression<Func<Receipt, bool>> predicate,
                int skip,
                int? take,
                bool asNoTracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>>? include,
                CancellationToken ct) => include!(receipts.AsQueryable().Where(predicate))
                    .Skip(skip).Take(take!.Value).ToList());

        return receiptRepository;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(item => item.Receipt).Returns(receiptRepository.Object);
        return unitOfWork;
    }

    private static IServiceScopeFactory CreateScopeFactory(Mock<IReceiptRepository> receipts, Mock<IReceiptRefreshWorkflow> workflow)
    {
        Mock<IServiceProvider> provider = new(MockBehavior.Strict);
        provider.Setup(item => item.GetService(typeof(IUnitOfWork))).Returns(CreateUnitOfWork(receipts).Object);
        provider.Setup(item => item.GetService(typeof(IReceiptRefreshWorkflow))).Returns(workflow.Object);
        Mock<IServiceScopeFactory> factory = new(MockBehavior.Strict);
        factory.Setup(item => item.CreateScope()).Returns(() =>
        {
            Mock<IServiceScope> scope = new(MockBehavior.Strict);
            scope.SetupGet(item => item.ServiceProvider).Returns(provider.Object);
            scope.Setup(item => item.Dispose());
            return scope.Object;
        });

        return factory.Object;
    }

    private static Mock<TimeProvider> CreateTimeProvider(DateTime utcNow)
    {
        Mock<TimeProvider> timeProvider = new(MockBehavior.Strict);
        timeProvider.Setup(provider => provider.GetUtcNow()).Returns(new DateTimeOffset(utcNow));
        timeProvider.SetupGet(provider => provider.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider;
    }
}
