using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class UpdateAccountUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesAccountAndNormalizesColor_WhenOwnerOwnsAccount()
    {
        Guid ownerUserId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Account current = TestAccountFactory.Create(accountId, ownerUserId, "Old", colorHex: "#000000");

        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Account, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UpdateAccountUseCase useCase = new(unitOfWork.Object);

        UpdateAccountRequest request = new()
        {
            AccountId = accountId,
            Name = "New",
            Description = "Updated",
            ColorHex = "#a1b2c3",
            IsActive = false
        };

        var result = await useCase.ExecuteAsync(ownerUserId, request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New", current.Name);
        Assert.Equal("Updated", current.Description);
        Assert.Equal("#A1B2C3", current.ColorHex);
        Assert.True(current.IsArchived);
        Assert.Equal(current.Id, result.Data?.Id);
        Assert.Equal("Updated", result.Data?.Description);
        Assert.False(result.Data?.IsActive);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenOwnerDoesNotOwnAccount()
    {
        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Account, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        UpdateAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), new UpdateAccountRequest
        {
            AccountId = Guid.NewGuid(),
            Name = "New",
            ColorHex = "#AABBCC",
            IsActive = true
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenAccountIdIsEmpty()
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        UpdateAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), new UpdateAccountRequest
        {
            AccountId = Guid.Empty,
            Name = "New",
            ColorHex = "#AABBCC",
            IsActive = true
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenColorIsInvalid()
    {
        Guid ownerUserId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Account current = TestAccountFactory.Create(accountId, ownerUserId, "Old", colorHex: "#000000");

        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Account, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        UpdateAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(ownerUserId, new UpdateAccountRequest
        {
            AccountId = accountId,
            Name = "New",
            ColorHex = "red",
            IsActive = true
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }
}
