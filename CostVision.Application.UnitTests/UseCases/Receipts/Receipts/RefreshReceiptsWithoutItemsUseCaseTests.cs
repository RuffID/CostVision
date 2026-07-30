using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Receipts;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class RefreshReceiptsWithoutItemsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_RefreshesOnlyReceiptsWithoutItems()
    {
        Receipt receiptWithoutItems = new() { Id = Guid.NewGuid() };
        Receipt receiptWithItems = new() { Id = Guid.NewGuid() };
        Product product = new() { Name = "Product", NormalizedName = "PRODUCT" };
        Assert.True(ReceiptItem.TryCreate(1, 1, 1, 0, default, default, default, product, null, out ReceiptItem? item, out string? itemError), itemError);
        Assert.True(receiptWithItems.TryAddItem(item!, out itemError), itemError);
        List<Receipt> receipts = [receiptWithoutItems, receiptWithItems];
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository(receipts);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow
            .Setup(workflow => workflow.RefreshAsync(receiptWithoutItems, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(receiptWithoutItems));
        Mock<ILogger<RefreshReceiptsWithoutItemsUseCase>> logger = new();
        RefreshReceiptsWithoutItemsUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object, refreshWorkflow.Object, logger.Object);

        await useCase.ExecuteAsync(CancellationToken.None);

        refreshWorkflow.Verify(workflow => workflow.RefreshAsync(receiptWithoutItems, It.IsAny<CancellationToken>()), Times.Once);
        refreshWorkflow.Verify(workflow => workflow.RefreshAsync(receiptWithItems, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAndLogsWarning_WhenReceiptRefreshFails()
    {
        Receipt failedReceipt = new() { Id = Guid.NewGuid() };
        Receipt successfulReceipt = new() { Id = Guid.NewGuid() };
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository([failedReceipt, successfulReceipt]);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow
            .Setup(workflow => workflow.RefreshAsync(failedReceipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Fail(502, "External API error"));
        refreshWorkflow
            .Setup(workflow => workflow.RefreshAsync(successfulReceipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(successfulReceipt));
        Mock<ILogger<RefreshReceiptsWithoutItemsUseCase>> logger = new();
        RefreshReceiptsWithoutItemsUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object, refreshWorkflow.Object, logger.Object);

        await useCase.ExecuteAsync(CancellationToken.None);

        refreshWorkflow.Verify(workflow => workflow.RefreshAsync(failedReceipt, It.IsAny<CancellationToken>()), Times.Once);
        refreshWorkflow.Verify(workflow => workflow.RefreshAsync(successfulReceipt, It.IsAny<CancellationToken>()), Times.Once);
        logger.Verify(
            log => log.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(failedReceipt.Id.ToString())),
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
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((
                Expression<Func<Receipt, bool>> predicate,
                int skip,
                int? take,
                bool asNoTracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>>? include,
                CancellationToken ct) => receipts.Where(predicate.Compile()).ToList());

        return receiptRepository;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        return unitOfWork;
    }
}
