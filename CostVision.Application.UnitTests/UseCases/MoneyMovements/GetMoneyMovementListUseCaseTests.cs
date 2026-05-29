using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Dtos.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class GetMoneyMovementListUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task GetMoneyMovementList_ReturnsSortedDtosAndAvailableReceiptCount()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        DateTime date = new(2026, 5, 10);
        MoneyMovement first = CreateMovement(Guid.NewGuid(), accountId, 100, date.AddHours(11), userId);
        first.Account = CreateAccount(accountId, userId, "Wallet");
        first.PerformedByUser = new User { Id = userId, Name = "Ivan" };
        MoneyMovement second = CreateMovement(Guid.NewGuid(), accountId, 50, date.AddHours(10), userId);
        second.Account = first.Account;
        Receipt matchingReceipt = CreateReceipt(Guid.NewGuid(), userId, accountId, 100, date.AddHours(12));

        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([second, first]);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepository();
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([matchingReceipt]);
        GetMoneyMovementListUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository).Object);

        var result = await useCase.ExecuteAsync(userId, date, date, accountId, CancellationToken.None);

        Assert.True(result.Success);
        List<MoneyMovementDto> data = result.Data!;
        Assert.Equal([first.Id, second.Id], data.Select(item => item.Id).ToList());
        Assert.Equal(1, data[0].AvailableReceiptCount);
        Assert.Equal(0, data[1].AvailableReceiptCount);
        Assert.Equal("Wallet", data[0].AccountName);
    }

    [Fact]
    public async Task GetMoneyMovementList_ReturnsBadRequest_WhenPeriodIsInvalid()
    {
        GetMoneyMovementListUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: CreateMoneyMovementRepository(),
            receiptRepository: CreateReceiptRepository()).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), new DateTime(2026, 5, 2), new DateTime(2026, 5, 1), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public async Task GetMoneyMovementList_ReturnsEmptyList_WhenNoMovementsExist()
    {
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
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
        GetMoneyMovementListUseCase useCase = new(CreateUnitOfWork(
            moneyMovementRepository: moneyMovementRepository,
            receiptRepository: receiptRepository).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }
}
