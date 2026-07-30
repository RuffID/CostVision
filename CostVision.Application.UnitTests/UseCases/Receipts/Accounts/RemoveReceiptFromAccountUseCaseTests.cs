using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class RemoveReceiptFromAccountUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_RemovesLink_WhenReceiptHasMultipleAccounts()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        ReceiptAccount link = new() { AccountId = accountId, Account = new Account { Id = accountId, CreatedByUserId = userId } };
        Receipt receipt = new()
        {
            Id = receiptId,
            Accounts = [link, new ReceiptAccount { AccountId = Guid.NewGuid(), Account = new Account { CreatedByUserId = userId } }]
        };
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository(receipt);
        Mock<IReceiptAccountRepository> receiptAccountRepository = new(MockBehavior.Strict);
        receiptAccountRepository.Setup(repository => repository.Delete(link));
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(receiptRepository, receiptAccountRepository);
        RemoveReceiptFromAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(accountId, receiptId, userId, CancellationToken.None);

        Assert.True(result.Success);
        receiptAccountRepository.Verify(repository => repository.Delete(link), Times.Once);
        receiptRepository.Verify(repository => repository.Delete(It.IsAny<Receipt>()), Times.Never);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DeletesReceipt_WhenRemovingLastAccountLink()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Receipt receipt = new()
        {
            Id = Guid.NewGuid(),
            Accounts = [new ReceiptAccount { AccountId = accountId, Account = new Account { Id = accountId, CreatedByUserId = userId } }]
        };
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository(receipt);
        receiptRepository.Setup(repository => repository.Delete(receipt));
        Mock<IReceiptAccountRepository> receiptAccountRepository = new(MockBehavior.Strict);
        RemoveReceiptFromAccountUseCase useCase = new(CreateUnitOfWork(receiptRepository, receiptAccountRepository).Object);

        var result = await useCase.ExecuteAsync(accountId, receipt.Id, userId, CancellationToken.None);

        Assert.True(result.Success);
        receiptRepository.Verify(repository => repository.Delete(receipt), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenUserCannotModifyAccount()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Account account = new()
        {
            Id = accountId,
            CreatedByUserId = Guid.NewGuid()
        };
        account.TryAddMember(userId, AccountAccessRole.Viewer, out _, out _);
        Receipt receipt = new()
        {
            Id = Guid.NewGuid(),
            Accounts = [new ReceiptAccount
            {
                AccountId = accountId,
                Account = account
            }]
        };
        RemoveReceiptFromAccountUseCase useCase = new(CreateUnitOfWork(CreateReceiptRepository(receipt), new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object);

        var result = await useCase.ExecuteAsync(accountId, receipt.Id, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenReceiptDoesNotExist()
    {
        RemoveReceiptFromAccountUseCase useCase = new(CreateUnitOfWork(CreateReceiptRepository(null), new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    private static Mock<IReceiptRepository> CreateReceiptRepository(Receipt? receipt)
    {
        Mock<IReceiptRepository> repository = new(MockBehavior.Strict);
        repository
            .Setup(item => item.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        return repository;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository, Mock<IReceiptAccountRepository> receiptAccountRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.ReceiptAccount).Returns(receiptAccountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return unitOfWork;
    }
}
