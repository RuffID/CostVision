using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class DeleteReceiptUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DeletesReceipt_WhenCurrentUserCreatedIt()
    {
        Guid userId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Receipt receipt = TestReceiptFactory.Create(receiptId, userId);

        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemByIdAsync(receiptId, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        receiptRepository.Setup(repository => repository.Delete(receipt));

        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(receiptRepository);
        DeleteReceiptUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(receiptId, new User { Id = userId }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data);
        receiptRepository.Verify(repository => repository.Delete(receipt), Times.Once);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenCurrentUserDidNotCreateReceipt()
    {
        Guid receiptId = Guid.NewGuid();
        Receipt receipt = TestReceiptFactory.Create(receiptId);

        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemByIdAsync(receiptId, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        DeleteReceiptUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        var result = await useCase.ExecuteAsync(receiptId, new User { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenReceiptDoesNotExist()
    {
        Guid receiptId = Guid.NewGuid();
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemByIdAsync(receiptId, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Receipt?)null);

        DeleteReceiptUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        var result = await useCase.ExecuteAsync(receiptId, new User { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenReceiptIdIsEmpty()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        DeleteReceiptUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        var result = await useCase.ExecuteAsync(Guid.Empty, new User { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return unitOfWork;
    }
}
