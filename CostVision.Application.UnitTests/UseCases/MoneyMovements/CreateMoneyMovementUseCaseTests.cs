using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class CreateMoneyMovementUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task CreateMoneyMovement_CreatesManualMovement_WhenRequestIsValid()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid performerId = Guid.NewGuid();
        MoneyMovement? createdMovement = null;
        MoneyMovement loadedMovement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(),
            accountId,
            125.50m,
            new DateTime(2026, 5, 10),
            userId,
            "Card",
            "Ivan",
            performedByUserId: performerId);
        loadedMovement.Id = Guid.NewGuid();

        Mock<IAccountMemberRepository> accountMemberRepository = CreateAccountMemberRepositorySequence(
        [
            CreateMember(accountId, userId, AccountAccessRole.Editor),
            CreateMember(accountId, performerId, AccountAccessRole.Viewer)
        ]);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository.Setup(repository => repository.Create(It.IsAny<MoneyMovement>()))
            .Callback<MoneyMovement>(movement => createdMovement = movement);
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(loadedMovement);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(
            accountMemberRepository: accountMemberRepository,
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository,
            setupSaveChanges: true,
            setupTransaction: true);
        CreateMoneyMovementUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new CreateMoneyMovementRequest
        {
            AccountId = accountId,
            Amount = -125.50m,
            OccurredAt = new DateTime(2026, 5, 10),
            Comment = "  groceries  ",
            PerformedByUserId = performerId
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(createdMovement);
        Assert.Equal(accountId, createdMovement.AccountId);
        Assert.Equal(125.50m, createdMovement.Amount);
        Assert.Equal(MoneyMovementType.Expense, createdMovement.Type);
        Assert.Equal("groceries", createdMovement.Comment);
        Assert.Equal(userId, createdMovement.CreatedByUserId);
        Assert.Equal(performerId, createdMovement.PerformedByUserId);
        Assert.Equal(MoneyMovementSource.Manual, createdMovement.Source);
        Assert.Equal("Card", result.Data?.AccountName);
        unitOfWork.Verify(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateMoneyMovement_CreatesExactReceiptLink_WhenSingleCandidateExists()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid movementId = Guid.NewGuid();
        DateTime occurredAt = new(2026, 5, 10, 12, 0, 0);
        Receipt receipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 125.50m, occurredAt.AddHours(1));
        MoneyMovement? createdMovement = null;
        MoneyMovementReceipt? createdLink = null;
        MoneyMovement loadedMovement = TestMoneyMovementFactory.CreateManual(
            movementId,
            accountId,
            125.50m,
            occurredAt,
            userId,
            "Card",
            type: MoneyMovementType.Expense);

        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository.Setup(repository => repository.Create(It.IsAny<MoneyMovement>()))
            .Callback<MoneyMovement>(movement =>
            {
                movement.Id = movementId;
                createdMovement = movement;
            });
        moneyMovementRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([loadedMovement]);
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(loadedMovement);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([receipt]);
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository.Setup(repository => repository.Create(It.IsAny<MoneyMovementReceipt>()))
            .Callback<MoneyMovementReceipt>(link => createdLink = link);
        CreateMoneyMovementUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            moneyMovementReceiptRepository: linkRepository,
            receiptRepository: receiptRepository,
            setupSaveChanges: true,
            setupTransaction: true).Object);

        var result = await useCase.ExecuteAsync(new CreateMoneyMovementRequest
        {
            AccountId = accountId,
            Amount = -125.50m,
            OccurredAt = occurredAt
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(createdMovement);
        Assert.NotNull(createdLink);
        Assert.Equal(movementId, createdLink.MoneyMovementId);
        Assert.Equal(receipt.Id, createdLink.ReceiptId);
    }

    [Theory]
    [InlineData(0, ServiceErrorType.Validation)]
    [InlineData(100, ServiceErrorType.NotFound)]
    public async Task CreateMoneyMovement_ReturnsError_WhenAmountOrAccountAccessIsInvalid(decimal amount, ServiceErrorType expectedErrorType)
    {
        Guid accountId = Guid.NewGuid();
        Mock<IAccountMemberRepository> accountMemberRepository = expectedErrorType == ServiceErrorType.NotFound
            ? CreateAccountMemberRepositorySequence([(AccountMember?)null])
            : CreateAccountMemberRepositorySequence([]);
        CreateMoneyMovementUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: accountMemberRepository,
            moneyMovementRepository: CreateMoneyMovementRepository()).Object);

        var result = await useCase.ExecuteAsync(new CreateMoneyMovementRequest
        {
            AccountId = accountId,
            Amount = amount,
            OccurredAt = new DateTime(2026, 5, 10)
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expectedErrorType, result.Error?.Type);
    }

    [Fact]
    public async Task CreateMoneyMovement_ReturnsForbidden_WhenAccountRoleIsViewer()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Mock<IAccountMemberRepository> accountMemberRepository = CreateAccountMemberRepositorySequence(
        [
            CreateMember(accountId, userId, AccountAccessRole.Viewer)
        ]);
        CreateMoneyMovementUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: accountMemberRepository,
            moneyMovementRepository: CreateMoneyMovementRepository()).Object);

        var result = await useCase.ExecuteAsync(new CreateMoneyMovementRequest
        {
            AccountId = accountId,
            Amount = 10,
            OccurredAt = new DateTime(2026, 5, 10)
        }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task CreateMoneyMovement_ReturnsBadRequest_WhenDateIsDefault()
    {
        CreateMoneyMovementUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([]),
            moneyMovementRepository: CreateMoneyMovementRepository()).Object);

        var result = await useCase.ExecuteAsync(new CreateMoneyMovementRequest
        {
            AccountId = Guid.NewGuid(),
            Amount = 10,
            OccurredAt = default
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }
}
