using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class SaveManualReceiptUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsAccessError_BeforeSavingReceipt()
    {
        Guid accountId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Mock<IValidateReceiptCreationAccessUseCase> accessUseCase = new(MockBehavior.Strict);
        accessUseCase.Setup(item => item.ExecuteAsync(accountId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Fail(ServiceErrorType.Forbidden, "Нет доступа."));
        SaveManualReceiptUseCase useCase = new(
            new Mock<IUnitOfWork>(MockBehavior.Strict).Object,
            accessUseCase.Object);

        ServiceResult<AddReceiptManualResponse> result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            AccountId = accountId,
            Receipt = CreateInput()
        }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
        Assert.Equal("Нет доступа.", result.Error?.Message);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesPendingManualReceiptAndLinksAccount()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Receipt? createdReceipt = null;
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, null);
        receiptRepository.Setup(repository => repository.Create(It.IsAny<Receipt>()))
            .Callback<Receipt>(receipt => createdReceipt = receipt);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict));
        SaveManualReceiptUseCase useCase = new(unitOfWork.Object, CreateAccessUseCase().Object);

        ServiceResult<AddReceiptManualResponse> result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            AccountId = accountId,
            Receipt = CreateInput()
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ManualReceiptOutcome.Created, result.Data?.Outcome);
        Assert.Null(result.Data?.Message);
        Assert.NotNull(createdReceipt);
        Assert.Equal(userId, createdReceipt.CreatedByUserId);
        Assert.Equal(accountId, createdReceipt.Accounts.Single().AccountId);
        Assert.Equal(createdReceipt.Id, result.Data?.Receipt?.Id);
        Assert.Equal(ReceiptRefreshStatus.Pending, createdReceipt.RefreshStatus);
        Assert.Equal(0, createdReceipt.RefreshAttemptCount);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AddsExistingReceiptToAccount_WhenReceiptExistsForUserButNotAccount()
    {
        Guid accountId = Guid.NewGuid();
        using CancellationTokenSource cancellationTokenSource = new();
        CancellationToken ct = cancellationTokenSource.Token;
        Receipt existingReceipt = TestReceiptFactory.Create(Guid.NewGuid());
        Receipt loadedReceipt = TestReceiptFactory.Create(existingReceipt.Id);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, existingReceipt, loadedReceipt);
        Mock<IReceiptAccountRepository> receiptAccountRepository = new(MockBehavior.Strict);
        receiptAccountRepository.Setup(repository => repository.Create(It.IsAny<ReceiptAccount>()));
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(receiptRepository, receiptAccountRepository);
        SaveManualReceiptUseCase useCase = new(unitOfWork.Object, CreateAccessUseCase().Object);

        ServiceResult<AddReceiptManualResponse> result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            AccountId = accountId,
            Receipt = CreateInput()
        }, Guid.NewGuid(), ct);

        Assert.True(result.Success);
        Assert.Equal(ManualReceiptOutcome.AddedToAccount, result.Data?.Outcome);
        Assert.Equal(loadedReceipt.Id, result.Data?.Receipt?.Id);
        Assert.NotNull(result.Data?.Message);
        receiptAccountRepository.Verify(repository => repository.Create(It.Is<ReceiptAccount>(link => link.AccountId == accountId && link.ReceiptId == existingReceipt.Id)), Times.Once);
        unitOfWork.Verify(item => item.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), ct), Times.Once);
        unitOfWork.Verify(item => item.SaveChangesAsync(ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDuplicateError_WhenReceiptAlreadyExistsInAccount()
    {
        Receipt existingReceipt = TestReceiptFactory.Create(Guid.NewGuid());
        Receipt loadedReceipt = TestReceiptFactory.Create(existingReceipt.Id);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(existingReceipt, loadedReceipt);
        SaveManualReceiptUseCase useCase = new(CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object, CreateAccessUseCase().Object);

        ServiceResult<AddReceiptManualResponse> result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            AccountId = Guid.NewGuid(),
            Receipt = CreateInput()
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ManualReceiptOutcome.AlreadyExistsInAccount, result.Data?.Outcome);
        Assert.Equal(loadedReceipt.Id, result.Data?.Receipt?.Id);
        Assert.NotNull(result.Data?.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailure_WhenReceiptDataIsInvalid()
    {
        SaveManualReceiptUseCase useCase = new(
            new Mock<IUnitOfWork>(MockBehavior.Strict).Object,
            new Mock<IValidateReceiptCreationAccessUseCase>(MockBehavior.Strict).Object);
        ManualReceiptInput input = CreateInput();
        input.FiscalDriveNumber = string.Empty;

        ServiceResult<AddReceiptManualResponse> result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            Receipt = input
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
        Assert.Null(result.Data);
    }

    private static ManualReceiptInput CreateInput()
    {
        return new ManualReceiptInput
        {
            FiscalDocumentNumber = "fd",
            FiscalDriveNumber = "fn",
            FiscalSign = "fp",
            Sum = 100,
            DateTime = new DateTime(2026, 1, 1),
            OperationType = ReceiptOperationType.Income
        };
    }

    private static Mock<IReceiptRepository> CreateReceiptRepositoryForSequence(params Receipt?[] receipts)
    {
        Mock<IReceiptRepository> repository = new(MockBehavior.Strict);
        var sequence = repository
            .SetupSequence(item => item.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Receipt, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Receipt>, IQueryable<Receipt>>?>(),
                It.IsAny<CancellationToken>()));
        foreach (Receipt? receipt in receipts)
            sequence = sequence.ReturnsAsync(receipt);
        return repository;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IReceiptRepository> receiptRepository, Mock<IReceiptAccountRepository> receiptAccountRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Receipt).Returns(receiptRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.ReceiptAccount).Returns(receiptAccountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        unitOfWork.Setup(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));
        return unitOfWork;
    }

    private static Mock<IValidateReceiptCreationAccessUseCase> CreateAccessUseCase()
    {
        Mock<IValidateReceiptCreationAccessUseCase> useCase = new(MockBehavior.Strict);
        useCase.Setup(item => item.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
        return useCase;
    }
}
