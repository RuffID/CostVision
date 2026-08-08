using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Dtos.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class GetReceiptListUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsReceiptsFromRepository_WhenPeriodIsValid()
    {
        User currentUser = TestUserFactory.Create();
        List<Receipt> receipts = [TestReceiptFactory.Create(Guid.NewGuid(), currentUser.Id)];
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(receipts);
        GetReceiptListUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        ServiceResult<List<ReceiptDto>> result = await useCase.ExecuteAsync(currentUser, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);

        Assert.True(result.Success);
        ReceiptDto receiptDto = Assert.Single(result.Data!);
        Assert.Equal(receipts[0].Id, receiptDto.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenDateToIsBeforeDateFrom()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        GetReceiptListUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        ServiceResult<List<ReceiptDto>> result = await useCase.ExecuteAsync(TestUserFactory.Create(), new DateTime(2026, 2, 1), new DateTime(2026, 1, 1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyList_WhenRepositoryReturnsNoReceipts()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        GetReceiptListUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        ServiceResult<List<ReceiptDto>> result = await useCase.ExecuteAsync(TestUserFactory.Create(), new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsGroupedAndSortedReceiptDtos_WhenRepositoryReturnsReceipts()
    {
        User currentUser = TestUserFactory.Create();
        DateTime oldDate = new(2026, 1, 3);
        DateTime newDate = new(2026, 1, 5);
        Receipt firstDuplicate = CreateReceipt(currentUser.Id, oldDate, "1", "1", "1");
        Receipt secondDuplicate = CreateReceipt(currentUser.Id, oldDate, "1", "1", "1");
        Receipt newestReceipt = CreateReceipt(currentUser.Id, newDate, "2", "2", "2");
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        receiptRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstDuplicate, newestReceipt, secondDuplicate]);
        GetReceiptListUseCase useCase = new(CreateUnitOfWork(receiptRepository).Object);

        ServiceResult<List<ReceiptDto>> result = await useCase.ExecuteAsync(currentUser, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(newestReceipt.Id, result.Data[0].Id);
        Assert.Equal(newDate, result.Data[0].DateTime);
        Assert.Equal(oldDate, result.Data[1].DateTime);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        return unitOfWork;
    }

    private static Receipt CreateReceipt(Guid currentUserId, DateTime dateTime, string fiscalDriveNumber, string fiscalDocumentNumber, string fiscalSign)
    {
        return TestReceiptFactory.Create(
            Guid.NewGuid(),
            currentUserId,
            dateTime,
            100,
            ReceiptOperationType.Income,
            fiscalDriveNumber: fiscalDriveNumber,
            fiscalDocumentNumber: fiscalDocumentNumber,
            fiscalSign: fiscalSign);
    }
}
