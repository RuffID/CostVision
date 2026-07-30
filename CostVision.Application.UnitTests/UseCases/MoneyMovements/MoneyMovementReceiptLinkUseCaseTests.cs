using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class MoneyMovementReceiptLinkUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task LinkMoneyMovementReceipt_CreatesLink_WhenAccessAndAccountMatch()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = CreateMovement(Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        Receipt receipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, DateTime.Today);
        MoneyMovementReceipt? createdLink = null;
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByIdAsync(movement.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovementReceipt?)null);
        linkRepository.Setup(repository => repository.Create(It.IsAny<MoneyMovementReceipt>()))
            .Callback<MoneyMovementReceipt>(link => createdLink = link);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: linkRepository,
            setupTransaction: true);
        LinkMoneyMovementReceiptUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new LinkMoneyMovementReceiptRequest
        {
            MoneyMovementId = movement.Id,
            ReceiptId = receipt.Id
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(createdLink);
        Assert.Equal(movement.Id, createdLink.MoneyMovementId);
        Assert.Equal(receipt.Id, createdLink.ReceiptId);
    }

    [Fact]
    public async Task LinkMoneyMovementReceipt_ReturnsConflict_WhenLinkAlreadyExists()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = CreateMovement(Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByIdAsync(movement.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReceipt(Guid.NewGuid(), userId, accountId, 100, DateTime.Today));
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestMoneyMovementFactory.CreateLink(movement.Id, Guid.NewGuid(), userId));
        LinkMoneyMovementReceiptUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: linkRepository).Object);

        var result = await useCase.ExecuteAsync(new LinkMoneyMovementReceiptRequest
        {
            MoneyMovementId = movement.Id,
            ReceiptId = Guid.NewGuid()
        }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.Error?.StatusCode);
    }

    [Fact]
    public async Task LinkMoneyMovementReceipt_ReturnsNotFound_WhenMovementOrReceiptIsMissing()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid movementId = Guid.NewGuid();
        Mock<IMoneyMovementRepository> missingMovementRepository = CreateMoneyMovementRepository();
        missingMovementRepository
            .Setup(repository => repository.GetItemByIdAsync(movementId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);
        LinkMoneyMovementReceiptUseCase missingMovementUseCase = new(CreateUnitOfWork(
            moneyMovementRepository: missingMovementRepository,
            receiptRepository: CreateReceiptRepository(),
            moneyMovementReceiptRepository: CreateMoneyMovementReceiptRepository()).Object);
        MoneyMovement movement = CreateMovement(movementId, accountId, 100, DateTime.Today, userId);
        Mock<IMoneyMovementRepository> movementRepository = CreateMoneyMovementRepository();
        movementRepository
            .Setup(repository => repository.GetItemByIdAsync(movementId, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IReceiptRepository> missingReceiptRepository = CreateReceiptRepository();
        missingReceiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Receipt?)null);
        LinkMoneyMovementReceiptUseCase missingReceiptUseCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: movementRepository,
            receiptRepository: missingReceiptRepository,
            moneyMovementReceiptRepository: CreateMoneyMovementReceiptRepository()).Object);

        var missingMovementResult = await missingMovementUseCase.ExecuteAsync(new LinkMoneyMovementReceiptRequest { MoneyMovementId = movementId, ReceiptId = Guid.NewGuid() }, userId, CancellationToken.None);
        var missingReceiptResult = await missingReceiptUseCase.ExecuteAsync(new LinkMoneyMovementReceiptRequest { MoneyMovementId = movementId, ReceiptId = Guid.NewGuid() }, userId, CancellationToken.None);

        Assert.Equal(404, missingMovementResult.Error?.StatusCode);
        Assert.Equal(404, missingReceiptResult.Error?.StatusCode);
    }

    [Fact]
    public async Task UnlinkMoneyMovementReceipt_DeletesLink_WhenAccessIsValid()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = CreateMovement(Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        MoneyMovementReceipt link = TestMoneyMovementFactory.CreateLink(
            movement.Id,
            Guid.NewGuid(),
            userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByIdAsync(movement.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);
        linkRepository.Setup(repository => repository.Delete(link));
        UnlinkMoneyMovementReceiptUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            moneyMovementReceiptRepository: linkRepository,
            setupTransaction: true).Object);

        var result = await useCase.ExecuteAsync(new UnlinkMoneyMovementReceiptRequest
        {
            MoneyMovementId = movement.Id,
            ReceiptId = link.ReceiptId
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        linkRepository.Verify(repository => repository.Delete(link), Times.Once);
    }

    [Fact]
    public async Task UnlinkMoneyMovementReceipt_ReturnsNotFound_WhenLinkIsMissing()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = CreateMovement(Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByIdAsync(movement.Id, true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovementReceipt?)null);
        UnlinkMoneyMovementReceiptUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            moneyMovementReceiptRepository: linkRepository).Object);

        var result = await useCase.ExecuteAsync(new UnlinkMoneyMovementReceiptRequest { MoneyMovementId = movement.Id, ReceiptId = Guid.NewGuid() }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }
}
