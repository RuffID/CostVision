using CostVision.Tests;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Application.Models.Responses.Results;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Enums.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class AutoLinkExactMoneyMovementReceiptsUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task AutoLinkExactMoneyMovementReceipts_CreatesOnlyUnambiguousLinks()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime date = new(2026, 5, 10);
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, date, userId, type: MoneyMovementType.Expense);
        Receipt matchingReceipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, date);
        Receipt duplicateReceipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 50, date);
        MoneyMovement duplicateMovement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 50, date, userId, type: MoneyMovementType.Expense);
        Account account = CreateAccount(accountId, userId);
        Assert.True(movement.TryMoveToAccount(account, out _));
        Assert.True(duplicateMovement.TryMoveToAccount(account, out _));
        List<MoneyMovementReceipt>? createdLinks = null;
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMovementCandidates([movement, duplicateMovement]);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptCandidates(
            [matchingReceipt, duplicateReceipt, CreateReceipt(Guid.NewGuid(), userId, accountId, 50, date)]);
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository.Setup(repository => repository.CreateRange(It.IsAny<IEnumerable<MoneyMovementReceipt>>()))
            .Callback<IEnumerable<MoneyMovementReceipt>>(links => createdLinks = links.ToList());
        Mock<ITestStorage> unitOfWork = CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: linkRepository,
            setupTransaction: true);
        AutoLinkExactMoneyMovementReceiptsUseCase useCase = TestUseCaseFactory.CreateAutoLinkExactMoneyMovementReceiptsUseCase(unitOfWork.Object);

        // Act
        ServiceResult<int> result = await useCase.ExecuteAsync(userId, date, date, accountId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Data);
        Assert.NotNull(createdLinks);
        Assert.Equal(movement.Id, createdLinks[0].MoneyMovementId);
        Assert.Equal(matchingReceipt.Id, createdLinks[0].ReceiptId);
        unitOfWork.Verify(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), System.Data.IsolationLevel.Serializable, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AutoLinkExactMoneyMovementReceipts_ReturnsZero_WhenNoUniqueCandidatesExist()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime date = new(2026, 5, 10);
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, date, userId, type: MoneyMovementType.Expense);
        Assert.True(movement.TryMoveToAccount(CreateAccount(accountId, userId), out _));
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMovementCandidates([movement]);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptCandidates([
                CreateReceipt(Guid.NewGuid(), userId, accountId, 100, date),
                CreateReceipt(Guid.NewGuid(), userId, accountId, 100, date)
            ]);
        AutoLinkExactMoneyMovementReceiptsUseCase useCase = TestUseCaseFactory.CreateAutoLinkExactMoneyMovementReceiptsUseCase(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: CreateMoneyMovementReceiptRepository(),
            setupTransaction: true).Object);

        // Act
        ServiceResult<int> result = await useCase.ExecuteAsync(userId, date, date, accountId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.Data);
    }

    [Theory]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 2)]
    [InlineData(true, false, 1)]
    public async Task AutoLink_UsesCurrentMembershipAndSelectedAccount(bool revoked, bool filterAccount, int expected)
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        DateTime date = new(2026, 5, 10);
        Account selected = CreateAccount(Guid.NewGuid(), Guid.NewGuid());
        Account other = CreateAccount(Guid.NewGuid(), userId);
        Assert.True(selected.TryAddMember(userId, AccountAccessRole.Editor, out _, out _));
        MoneyMovement selectedMovement = TestMoneyMovementFactory.CreateManual(
            accountId: selected.Id, amount: 100, occurredAt: date, userId: userId, type: MoneyMovementType.Expense);
        MoneyMovement otherMovement = TestMoneyMovementFactory.CreateManual(
            accountId: other.Id, amount: 100, occurredAt: date, userId: userId, type: MoneyMovementType.Expense);
        Assert.True(selectedMovement.TryMoveToAccount(selected, out _));
        Assert.True(otherMovement.TryMoveToAccount(other, out _));
        Receipt selectedReceipt = CreateReceipt(Guid.NewGuid(), userId, selected.Id, 100, date);
        Receipt otherReceipt = CreateReceipt(Guid.NewGuid(), userId, other.Id, 100, date);
        if (revoked)
        {
            Assert.True(selected.TryRemoveMember(userId, out _, out _));
        }

        Guid? accountId = null;
        if (filterAccount)
        {
            accountId = selected.Id;
        }

        List<MoneyMovementReceipt> createdLinks = new();
        Mock<IMoneyMovementReceiptRepository> links = CreateMoneyMovementReceiptRepository();
        links.Setup(repository => repository.CreateRange(It.IsAny<IEnumerable<MoneyMovementReceipt>>()))
            .Callback<IEnumerable<MoneyMovementReceipt>>(items => createdLinks.AddRange(items));
        Mock<ITestStorage> storage = CreateUnitOfWork(
            moneyMovementRepository: CreateMovementCandidates([selectedMovement, otherMovement]),
            receiptRepository: CreateReceiptCandidates([selectedReceipt, otherReceipt]),
            moneyMovementReceiptRepository: links,
            setupTransaction: true);
        AutoLinkExactMoneyMovementReceiptsUseCase useCase = TestUseCaseFactory.CreateAutoLinkExactMoneyMovementReceiptsUseCase(storage.Object);

        // Act
        ServiceResult<int> result = await useCase.ExecuteAsync(userId, date, date, accountId, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expected, result.Data);
        Assert.Equal(expected, createdLinks.Count);
        if (revoked)
        {
            Assert.DoesNotContain(createdLinks, link => link.MoneyMovementId == selectedMovement.Id);
        }

        if (filterAccount)
        {
            Assert.DoesNotContain(createdLinks, link => link.MoneyMovementId == otherMovement.Id);
        }

        if (expected == 0)
        {
            storage.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            storage.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    private static Mock<IMoneyMovementRepository> CreateMovementCandidates(IReadOnlyList<MoneyMovement> items)
    {
        Mock<IMoneyMovementRepository> repository = CreateMoneyMovementRepository();
        repository.Setup(item => item.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(), It.IsAny<int>(), It.IsAny<int?>(), true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<MoneyMovement, bool>> predicate, int skip, int? take, bool noTracking,
                Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>? include, CancellationToken ct) =>
                items.AsQueryable().Where(predicate).Skip(skip).Take(take ?? int.MaxValue).ToList());

        return repository;
    }

    private static Mock<IReceiptRepository> CreateReceiptCandidates(IReadOnlyList<Receipt> items)
    {
        Mock<IReceiptRepository> repository = CreateReceiptRepository();
        repository.Setup(item => item.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(), It.IsAny<int>(), It.IsAny<int?>(), true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Receipt, bool>> predicate, int skip, int? take, bool noTracking,
                Func<IQueryable<Receipt>, IQueryable<Receipt>>? include, CancellationToken ct) =>
                items.AsQueryable().Where(predicate).Skip(skip).Take(take ?? int.MaxValue).ToList());

        return repository;
    }
}
