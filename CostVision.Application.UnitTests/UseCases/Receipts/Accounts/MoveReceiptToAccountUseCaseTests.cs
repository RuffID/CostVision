using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class MoveReceiptToAccountUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_MovesReceiptBetweenAccessibleAccounts()
    {
        Guid userId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Guid sourceAccountId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();
        Account sourceAccount = TestAccountFactory.Create(sourceAccountId, userId);
        Account targetAccount = TestAccountFactory.Create(targetAccountId, userId);
        Receipt receipt = CreateReceipt(receiptId, userId);
        Assert.True(receipt.TryAddAccount(sourceAccount, out ReceiptAccount? sourceLink, out string? linkError), linkError);
        ReceiptAccount? createdLink = null;

        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .SetupSequence(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt)
            .ReturnsAsync((Receipt?)null);
        Mock<IAccountRepository> accountRepository = CreateAccountRepository(targetAccount);
        Mock<IReceiptAccountRepository> receiptAccountRepository = new(MockBehavior.Strict);
        receiptAccountRepository.Setup(repository => repository.Delete(sourceLink!));
        receiptAccountRepository.Setup(repository => repository.Create(It.IsAny<ReceiptAccount>()))
            .Callback<ReceiptAccount>(link => createdLink = link);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(receiptRepository, accountRepository, receiptAccountRepository);
        MoveReceiptToAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(sourceAccountId, targetAccountId, receiptId, userId, CancellationToken.None);

        Assert.True(result.Success);
        receiptAccountRepository.Verify(repository => repository.Delete(sourceLink!), Times.Once);
        Assert.NotNull(createdLink);
        Assert.Equal(receiptId, createdLink.ReceiptId);
        Assert.Equal(targetAccountId, createdLink.AccountId);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenReceiptWasCreatedByAnotherUser()
    {
        Guid receiptId = Guid.NewGuid();
        Receipt receipt = CreateReceipt(receiptId, Guid.NewGuid());
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        MoveReceiptToAccountUseCase useCase = new(CreateUnitOfWork(receiptRepository, CreateAccountRepository(null), new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), receiptId, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenReceiptAlreadyLinkedToTargetAccount()
    {
        Guid userId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Guid sourceAccountId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();
        Receipt receipt = CreateReceipt(receiptId, userId);
        Account sourceAccount = TestAccountFactory.Create(sourceAccountId, userId);
        Account targetAccount = TestAccountFactory.Create(targetAccountId, userId);
        Assert.True(receipt.TryAddAccount(sourceAccount, out ReceiptAccount? sourceLink, out string? sourceError), sourceError);
        Assert.True(receipt.TryAddAccount(targetAccount, out ReceiptAccount? targetLink, out string? targetError), targetError);
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        MoveReceiptToAccountUseCase useCase = new(CreateUnitOfWork(receiptRepository, CreateAccountRepository(null), new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object);

        var result = await useCase.ExecuteAsync(sourceAccountId, targetAccountId, receiptId, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Conflict, result.Error?.Type);
    }

    private static Receipt CreateReceipt(Guid receiptId, Guid createdByUserId)
    {
        return TestReceiptFactory.Create(receiptId, createdByUserId);
    }

    private static Mock<IAccountRepository> CreateAccountRepository(Account? account)
    {
        Mock<IAccountRepository> repository = new(MockBehavior.Strict);
        if (account != null)
        {
            repository
                .Setup(item => item.GetItemByPredicateAsync(
                    It.IsAny<Expression<Func<Account, bool>>>(),
                    false,
                    It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
        }
        return repository;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository, Mock<IAccountRepository> accountRepository, Mock<IReceiptAccountRepository> receiptAccountRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.ReceiptAccount).Returns(receiptAccountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return unitOfWork;
    }
}
