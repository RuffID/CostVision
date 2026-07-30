using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class LinkedMoneyMovementsUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task GetLinkedMoneyMovementReceipts_ReturnsLinkedReceiptsSortedByDate()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        MoneyMovementReceipt oldLink = TestMoneyMovementFactory.CreateLink(
            movement,
            CreateReceipt(Guid.NewGuid(), userId, accountId, 10, new DateTime(2026, 5, 1)),
            userId);
        MoneyMovementReceipt newLink = TestMoneyMovementFactory.CreateLink(
            movement,
            CreateReceipt(Guid.NewGuid(), userId, accountId, 20, new DateTime(2026, 5, 2)),
            userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldLink, newLink]);
        GetLinkedMoneyMovementReceiptsUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            moneyMovementReceiptRepository: linkRepository).Object);

        var result = await useCase.ExecuteAsync(movement.Id, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([newLink.Receipt!.Id, oldLink.Receipt!.Id], result.Data!.Select(item => item.ReceiptId).ToList());
    }

    [Fact]
    public async Task GetLinkedMoneyMovementReceipts_ReturnsEmptyList_WhenNoLinksExist()
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
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        GetLinkedMoneyMovementReceiptsUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            moneyMovementReceiptRepository: linkRepository).Object);

        var result = await useCase.ExecuteAsync(movement.Id, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetLinkedReceiptMoneyMovements_ReturnsLinkedMovementsSortedByDate()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Receipt receipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, DateTime.Today);
        MoneyMovement oldMovement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 10, new DateTime(2026, 5, 1), userId, "Card");
        MoneyMovement newMovement = TestMoneyMovementFactory.CreateManual(
            Guid.NewGuid(), accountId, 20, new DateTime(2026, 5, 2), userId, "Card");
        MoneyMovementReceipt oldLink = TestMoneyMovementFactory.CreateLink(oldMovement, receipt, userId);
        MoneyMovementReceipt newLink = TestMoneyMovementFactory.CreateLink(newMovement, receipt, userId);
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
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([oldLink, newLink]);
        GetLinkedReceiptMoneyMovementsUseCase useCase = new(CreateUnitOfWork(
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: linkRepository).Object);

        var result = await useCase.ExecuteAsync(receipt.Id, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal([newMovement.Id, oldMovement.Id], result.Data!.Select(item => item.MoneyMovementId).ToList());
    }

    [Fact]
    public async Task GetLinkedReceiptMoneyMovements_ReturnsNotFound_WhenReceiptIsNotAccessible()
    {
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Receipt?)null);
        GetLinkedReceiptMoneyMovementsUseCase useCase = new(CreateUnitOfWork(
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: CreateMoneyMovementReceiptRepository()).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task GetLinkedReceiptMoneyMovements_ReturnsEmptyList_WhenNoLinksExist()
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
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovementReceipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovementReceipt>, IQueryable<MoneyMovementReceipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        GetLinkedReceiptMoneyMovementsUseCase useCase = new(CreateUnitOfWork(
            receiptRepository: receiptRepository,
            moneyMovementReceiptRepository: linkRepository).Object);

        var result = await useCase.ExecuteAsync(receipt.Id, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }
}
