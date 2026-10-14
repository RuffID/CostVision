using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class RefreshReceiptFromApiUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_RefreshesReceipt_WhenUserHasAccess()
    {
        // Arrange
        User currentUser = TestUserFactory.Create();
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid(), currentUser.Id);
        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        accessVerification.Setup(service => service.UserHasAccessToReceipt(currentUser, receipt)).Returns(true);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow.Setup(workflow => workflow.RefreshAsync(receipt, It.IsAny<CancellationToken>(), It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(receipt));
        RefreshReceiptFromApiUseCase useCase = new(CreateUnitOfWork(receipt).Object, accessVerification.Object, refreshWorkflow.Object);

        // Act
        var result = await useCase.ExecuteAsync(receipt.Id, currentUser, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(receipt.Id, result.Data?.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenUserHasNoAccess()
    {
        // Arrange
        User currentUser = TestUserFactory.Create();
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid());
        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        accessVerification.Setup(service => service.UserHasAccessToReceipt(currentUser, receipt)).Returns(false);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        RefreshReceiptFromApiUseCase useCase = new(CreateUnitOfWork(receipt).Object, accessVerification.Object, refreshWorkflow.Object);

        // Act
        var result = await useCase.ExecuteAsync(receipt.Id, currentUser, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenReceiptDoesNotExist()
    {
        // Arrange
        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        RefreshReceiptFromApiUseCase useCase = new(CreateUnitOfWork(null).Object, accessVerification.Object, refreshWorkflow.Object);

        // Act
        var result = await useCase.ExecuteAsync(Guid.NewGuid(), TestUserFactory.Create(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }


    [Fact]
    public async Task ExecuteAsync_ReturnsWorkflowError_WhenRefreshWorkflowFails()
    {
        // Arrange
        User currentUser = TestUserFactory.Create();
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid(), currentUser.Id);
        Mock<IReceiptAccessVerificationService> accessVerification = new(MockBehavior.Strict);
        accessVerification.Setup(service => service.UserHasAccessToReceipt(currentUser, receipt)).Returns(true);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow.Setup(workflow => workflow.RefreshAsync(receipt, It.IsAny<CancellationToken>(), It.IsAny<Guid>()))
            .ReturnsAsync(ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "Provider error"));
        RefreshReceiptFromApiUseCase useCase = new(CreateUnitOfWork(receipt).Object, accessVerification.Object, refreshWorkflow.Object);

        // Act
        var result = await useCase.ExecuteAsync(receipt.Id, currentUser, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.ExternalService, result.Error?.Type);
    }
    private static Mock<IUnitOfWork> CreateUnitOfWork(Receipt? receipt)
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, bool noTracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
                new[] { receipt }.FirstOrDefault(item => item != null && predicate.Compile()(item)));
        receiptRepository.Setup(repository => repository.GetItemsByPredicateAsync(
            It.IsAny<Expression<Func<Receipt, bool>>>(), It.IsAny<int>(), It.IsAny<int?>(), true,
            It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool tracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>> include, CancellationToken ct) =>
            {
                List<Receipt> available = [];
                if (receipt != null && predicate.Compile()(receipt))
                {
                    available.Add(receipt);
                }

                return Task.FromResult(available);
            });
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        return unitOfWork;
    }
}
