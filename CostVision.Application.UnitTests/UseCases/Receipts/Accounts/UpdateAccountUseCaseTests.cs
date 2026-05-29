using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
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
        Account current = new()
        {
            Id = accountId,
            Name = "Old",
            ColorHex = "#000000",
            CreatedByUserId = ownerUserId
        };

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

        Account requested = new()
        {
            Id = accountId,
            Name = "New",
            Description = "Updated",
            ColorHex = "#a1b2c3",
            IsArchived = true
        };

        var result = await useCase.ExecuteAsync(ownerUserId, requested, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New", current.Name);
        Assert.Equal("Updated", current.Description);
        Assert.Equal("#A1B2C3", current.ColorHex);
        Assert.True(current.IsArchived);
        Assert.Equal("#A1B2C3", requested.ColorHex);
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

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), new Account
        {
            Id = Guid.NewGuid(),
            Name = "New",
            ColorHex = "#AABBCC"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenColorIsInvalid()
    {
        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        UpdateAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), new Account
        {
            Id = Guid.NewGuid(),
            Name = "New",
            ColorHex = "red"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }
}
