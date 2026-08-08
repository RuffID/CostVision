using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class GetReceiptWithItemsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsReceipt_WhenUserHasAccess()
    {
        User currentUser = TestUserFactory.Create();
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid(), currentUser.Id);

        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        accessVerification.Setup(service => service.UserHasAccessToReceipt(currentUser, receipt)).Returns(true);

        GetReceiptWithItemsUseCase useCase = new(CreateUnitOfWork(receipt).Object, accessVerification.Object);

        var result = await useCase.ExecuteAsync(receipt.Id, currentUser, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(receipt.Id, result.Data?.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenUserHasNoAccess()
    {
        User currentUser = TestUserFactory.Create();
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid());

        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        accessVerification.Setup(service => service.UserHasAccessToReceipt(currentUser, receipt)).Returns(false);

        GetReceiptWithItemsUseCase useCase = new(CreateUnitOfWork(receipt).Object, accessVerification.Object);

        var result = await useCase.ExecuteAsync(receipt.Id, currentUser, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenReceiptDoesNotExist()
    {
        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        GetReceiptWithItemsUseCase useCase = new(CreateUnitOfWork(null).Object, accessVerification.Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), TestUserFactory.Create(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenReceiptIdIsEmpty()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        GetReceiptWithItemsUseCase useCase = new(unitOfWork.Object, accessVerification.Object);

        var result = await useCase.ExecuteAsync(Guid.Empty, TestUserFactory.Create(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Receipt? receipt)
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);

        return unitOfWork;
    }
}
