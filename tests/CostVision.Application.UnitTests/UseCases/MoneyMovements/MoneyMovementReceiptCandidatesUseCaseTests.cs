using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class MoneyMovementReceiptCandidatesUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task GetMoneyMovementReceiptCandidates_ReturnsSortedUnlinkedReceipts()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime occurredAt = new(2026, 5, 10, 12, 0, 0);
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, occurredAt, userId, "Card");
        Receipt nearest = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, occurredAt.AddMinutes(5));
        Receipt later = CreateReceipt(Guid.NewGuid(), userId, accountId, 101, occurredAt.AddMinutes(30));
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([later, nearest]);
        GetMoneyMovementReceiptCandidatesUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository).Object);

        var result = await useCase.ExecuteAsync(new GetMoneyMovementReceiptCandidatesRequest
        {
            MoneyMovementId = movement.Id,
            UseTimeWindow = true,
            TimeWindowHours = 1,
            UseAmountFilter = true
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([nearest.Id, later.Id], result.Data!.Select(item => item.ReceiptId).ToList());
    }

    [Fact]
    public async Task GetMoneyMovementReceiptCandidates_ReturnsBadRequest_WhenToleranceIsNegative()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        GetMoneyMovementReceiptCandidatesUseCase useCase = new(CreateUnitOfWork(moneyMovementRepository: moneyMovementRepository).Object);

        var result = await useCase.ExecuteAsync(new GetMoneyMovementReceiptCandidatesRequest
        {
            MoneyMovementId = movement.Id,
            AmountTolerance = -1
        }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    [Fact]
    public async Task GetReceiptMoneyMovementCandidates_ReturnsSortedUnlinkedMovements()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime receiptDate = new(2026, 5, 10, 12, 0, 0);
        Receipt receipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, receiptDate);
        MoneyMovement nearest = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, receiptDate.AddMinutes(5), userId, "Card");
        MoneyMovement later = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 101, receiptDate.AddMinutes(30), userId, "Card");
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([later, nearest]);
        GetReceiptMoneyMovementCandidatesUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository).Object);

        var result = await useCase.ExecuteAsync(new GetReceiptMoneyMovementCandidatesRequest
        {
            ReceiptId = receipt.Id,
            UseTimeWindow = true,
            TimeWindowHours = 1,
            UseAmountFilter = true
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([nearest.Id, later.Id], result.Data!.Select(item => item.MoneyMovementId).ToList());
    }

    [Fact]
    public async Task GetReceiptMoneyMovementCandidates_ReturnsBadRequest_WhenTimeWindowIsNegative()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Receipt receipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, DateTime.Today);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipt);
        GetReceiptMoneyMovementCandidatesUseCase useCase = new(CreateUnitOfWork(receiptRepository: receiptRepository).Object);

        var result = await useCase.ExecuteAsync(new GetReceiptMoneyMovementCandidatesRequest
        {
            ReceiptId = receipt.Id,
            UseTimeWindow = true,
            TimeWindowHours = -1
        }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }
}
