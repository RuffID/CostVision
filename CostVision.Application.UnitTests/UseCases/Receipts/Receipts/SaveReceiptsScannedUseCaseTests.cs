using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class SaveReceiptsScannedUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesReceiptFromValidQrAndRunsRefreshWorkflow()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Receipt? createdReceipt = null;
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, null);
        receiptRepository.Setup(repository => repository.Create(It.IsAny<Receipt>()))
            .Callback<Receipt>(receipt => createdReceipt = receipt);
        Mock<IQrParser> qrParser = new(MockBehavior.Strict);
        qrParser.Setup(parser => parser.Parse("qr")).Returns(CreateParsed());
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow.Setup(workflow => workflow.TryRefreshCreatedReceiptsAsync(It.IsAny<IEnumerable<Receipt>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SaveReceiptsScannedUseCase useCase = new(CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object, qrParser.Object, refreshWorkflow.Object);

        ReceiptScanResultSummary summary = await useCase.ExecuteAsync(new QrScanRequest
        {
            AccountId = accountId,
            Results = [new QrScanResult { DecodedText = "qr" }]
        }, userId, CancellationToken.None);

        Assert.Equal(1, summary.ScannedCount);
        Assert.Equal(1, summary.AddedToDbCount);
        Assert.Equal(0, summary.ErrorCount);
        Assert.NotNull(createdReceipt);
        Assert.Equal(userId, createdReceipt.CreatedByUserId);
        Assert.Equal(accountId, createdReceipt.Accounts.Single().AccountId);
        refreshWorkflow.Verify(workflow => workflow.TryRefreshCreatedReceiptsAsync(It.Is<IEnumerable<Receipt>>(receipts => receipts.Single() == createdReceipt), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorForInvalidQr()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        Mock<IQrParser> qrParser = new(MockBehavior.Strict);
        qrParser.Setup(parser => parser.Parse("bad")).Returns((QrParsed)null!);
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        SaveReceiptsScannedUseCase useCase = new(CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object, qrParser.Object, refreshWorkflow.Object);

        ReceiptScanResultSummary summary = await useCase.ExecuteAsync(new QrScanRequest
        {
            Results = [new QrScanResult { DecodedText = "bad" }]
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(1, summary.ScannedCount);
        Assert.Equal(0, summary.AddedToDbCount);
        Assert.Equal(1, summary.ErrorCount);
        Assert.NotNull(summary.Results.Single().ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_AddsExistingReceiptToAccount_WhenReceiptExistsForUserButNotAccount()
    {
        Guid accountId = Guid.NewGuid();
        Receipt existingReceipt = new() { Id = Guid.NewGuid() };
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, existingReceipt);
        Mock<IReceiptAccountRepository> receiptAccountRepository = new(MockBehavior.Strict);
        receiptAccountRepository.Setup(repository => repository.Create(It.IsAny<ReceiptAccount>()));
        Mock<IQrParser> qrParser = new(MockBehavior.Strict);
        qrParser.Setup(parser => parser.Parse("qr")).Returns(CreateParsed());
        Mock<IReceiptRefreshWorkflow> refreshWorkflow = new(MockBehavior.Strict);
        refreshWorkflow.Setup(workflow => workflow.TryRefreshCreatedReceiptsAsync(It.IsAny<IEnumerable<Receipt>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        SaveReceiptsScannedUseCase useCase = new(CreateUnitOfWork(receiptRepository, receiptAccountRepository).Object, qrParser.Object, refreshWorkflow.Object);

        ReceiptScanResultSummary summary = await useCase.ExecuteAsync(new QrScanRequest
        {
            AccountId = accountId,
            Results = [new QrScanResult { DecodedText = "qr" }]
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(1, summary.AddedToDbCount);
        receiptAccountRepository.Verify(repository => repository.Create(It.Is<ReceiptAccount>(link => link.AccountId == accountId && link.ReceiptId == existingReceipt.Id)), Times.Once);
    }

    private static QrParsed CreateParsed()
    {
        return new QrParsed
        {
            FiscalDocumentNumber = "fd",
            FiscalDriveNumber = "fn",
            FiscalSign = "fp",
            Sum = 100,
            DateTime = new DateTime(2026, 1, 1),
            OperationType = (int)ReceiptOperationType.Income
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
