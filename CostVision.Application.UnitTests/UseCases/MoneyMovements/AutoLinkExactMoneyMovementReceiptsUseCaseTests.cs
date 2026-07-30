using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.MoneyMovements;
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
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime date = new(2026, 5, 10);
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, date, userId, type: MoneyMovementType.Expense);
        Receipt matchingReceipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, date);
        Receipt duplicateReceipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 50, date);
        MoneyMovement duplicateMovement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 50, date, userId, type: MoneyMovementType.Expense);
        List<MoneyMovementReceipt>? createdLinks = null;
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([movement, duplicateMovement]);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([matchingReceipt, duplicateReceipt, CreateReceipt(Guid.NewGuid(), userId, accountId, 50, date)]);
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository.Setup(repository => repository.CreateRange(It.IsAny<IEnumerable<MoneyMovementReceipt>>()))
            .Callback<IEnumerable<MoneyMovementReceipt>>(links => createdLinks = links.ToList());
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: linkRepository,
            setupTransaction: true);
        AutoLinkExactMoneyMovementReceiptsUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(userId, date, date, accountId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data);
        Assert.NotNull(createdLinks);
        Assert.Equal(movement.Id, createdLinks[0].MoneyMovementId);
        Assert.Equal(matchingReceipt.Id, createdLinks[0].ReceiptId);
        unitOfWork.Verify(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AutoLinkExactMoneyMovementReceipts_ReturnsZero_WhenNoUniqueCandidatesExist()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime date = new(2026, 5, 10);
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, date, userId, type: MoneyMovementType.Expense);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([movement]);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                CreateReceipt(Guid.NewGuid(), userId, accountId, 100, date),
                CreateReceipt(Guid.NewGuid(), userId, accountId, 100, date)
            ]);
        AutoLinkExactMoneyMovementReceiptsUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: CreateMoneyMovementReceiptRepository(),
            setupTransaction: true).Object);

        var result = await useCase.ExecuteAsync(userId, date, date, accountId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data);
    }
}
