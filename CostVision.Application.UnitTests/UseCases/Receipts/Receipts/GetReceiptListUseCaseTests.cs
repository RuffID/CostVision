using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class GetReceiptListUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsReceiptsFromRepository_WhenPeriodIsValid()
    {
        User currentUser = new() { Id = Guid.NewGuid() };
        List<Receipt> receipts = [new Receipt { Id = Guid.NewGuid(), CreatedByUserId = currentUser.Id }];
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipts);
        GetReceiptListUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        var result = await useCase.ExecuteAsync(currentUser, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Same(receipts, result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenDateToIsBeforeDateFrom()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        GetReceiptListUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        var result = await useCase.ExecuteAsync(new User { Id = Guid.NewGuid() }, new DateTime(2026, 2, 1), new DateTime(2026, 1, 1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyList_WhenRepositoryReturnsNoReceipts()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        GetReceiptListUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        var result = await useCase.ExecuteAsync(new User { Id = Guid.NewGuid() }, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        return unitOfWork;
    }
}
