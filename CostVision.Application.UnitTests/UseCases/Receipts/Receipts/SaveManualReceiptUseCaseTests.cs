using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class SaveManualReceiptUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesManualReceiptLinksAccountAndRunsRefreshWorkflow()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Receipt? createdReceipt = null;
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, null);
        receiptRepository.Setup(repository => repository.Create(It.IsAny<Receipt>()))
            .Callback<Receipt>(receipt => createdReceipt = receipt);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow.Setup(workflow => workflow.TryRefreshCreatedReceiptAsync(It.IsAny<Receipt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Receipt receipt, CancellationToken _) => receipt);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict));
        SaveManualReceiptUseCase useCase = new(unitOfWork.Object, refreshWorkflow.Object);

        var result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            AccountId = accountId,
            Receipt = CreateInput()
        }, userId, CancellationToken.None);

        Assert.True(result.IsCreated);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(createdReceipt);
        Assert.Equal(userId, createdReceipt.CreatedByUserId);
        Assert.Equal(accountId, createdReceipt.Accounts.Single().AccountId);
        Assert.Same(createdReceipt, result.Receipt);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        refreshWorkflow.Verify(workflow => workflow.TryRefreshCreatedReceiptAsync(createdReceipt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AddsExistingReceiptToAccount_WhenReceiptExistsForUserButNotAccount()
    {
        Guid accountId = Guid.NewGuid();
        Receipt existingReceipt = TestReceiptFactory.Create(Guid.NewGuid());
        Receipt loadedReceipt = TestReceiptFactory.Create(existingReceipt.Id);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, existingReceipt, loadedReceipt);
        Mock<IReceiptAccountRepository> receiptAccountRepository = new(MockBehavior.Strict);
        receiptAccountRepository.Setup(repository => repository.Create(It.IsAny<ReceiptAccount>()));
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        SaveManualReceiptUseCase useCase = new(CreateUnitOfWork(receiptRepository, receiptAccountRepository).Object, refreshWorkflow.Object);

        var result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            AccountId = accountId,
            Receipt = CreateInput()
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.IsCreated);
        Assert.Same(loadedReceipt, result.Receipt);
        Assert.NotNull(result.ErrorMessage);
        receiptAccountRepository.Verify(repository => repository.Create(It.Is<ReceiptAccount>(link => link.AccountId == accountId && link.ReceiptId == existingReceipt.Id)), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDuplicateError_WhenReceiptAlreadyExistsInAccount()
    {
        Receipt existingReceipt = TestReceiptFactory.Create(Guid.NewGuid());
        Receipt loadedReceipt = TestReceiptFactory.Create(existingReceipt.Id);
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(existingReceipt, loadedReceipt);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        SaveManualReceiptUseCase useCase = new(CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object, refreshWorkflow.Object);

        var result = await useCase.ExecuteAsync(new ReceiptManualCreateRequest
        {
            AccountId = Guid.NewGuid(),
            Receipt = CreateInput()
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsCreated);
        Assert.Same(loadedReceipt, result.Receipt);
        Assert.NotNull(result.ErrorMessage);
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
        return unitOfWork;
    }
}
