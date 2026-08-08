using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.Models.Services.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.UseCases.Receipts.Receipts;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class SaveReceiptsScannedUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsAccessError_BeforeProcessingQrCodes()
    {
        Guid accountId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Mock<IValidateReceiptCreationAccessUseCase> accessUseCase = new(MockBehavior.Strict);
        accessUseCase.Setup(item => item.ExecuteAsync(accountId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Fail(ServiceErrorType.Forbidden, "Нет доступа."));
        SaveReceiptsScannedUseCase useCase = new(
            new Mock<IUnitOfWork>(MockBehavior.Strict).Object,
            accessUseCase.Object,
            new Mock<IQrParser>(MockBehavior.Strict).Object);

        ServiceResult<AddReceiptScanResponse> result = await useCase.ExecuteAsync(new QrScanRequest
        {
            AccountId = accountId,
            Results = [new QrScanResult { DecodedText = "qr" }]
        }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
        Assert.Equal("Нет доступа.", result.Error?.Message);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesPendingReceiptFromValidQr()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Receipt? createdReceipt = null;
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, null);
        receiptRepository.Setup(repository => repository.Create(It.IsAny<Receipt>()))
            .Callback<Receipt>(receipt => createdReceipt = receipt);
        Mock<IQrParser> qrParser = new(MockBehavior.Strict);
        qrParser.Setup(parser => parser.Parse("qr")).Returns(CreateParsed());
        SaveReceiptsScannedUseCase useCase = new(CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object, CreateAccessUseCase().Object, qrParser.Object);

        ServiceResult<AddReceiptScanResponse> result = await useCase.ExecuteAsync(new QrScanRequest
        {
            AccountId = accountId,
            Results = [new QrScanResult { DecodedText = "qr" }]
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data?.ScannedCount);
        Assert.Equal(1, result.Data?.AddedToDbCount);
        Assert.Equal(0, result.Data?.ErrorCount);
        Assert.NotNull(createdReceipt);
        Assert.Equal(userId, createdReceipt.CreatedByUserId);
        Assert.Equal(accountId, createdReceipt.Accounts.Single().AccountId);
        Assert.Equal(ReceiptRefreshStatus.Pending, createdReceipt.RefreshStatus);
        Assert.Equal(0, createdReceipt.RefreshAttemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsErrorForInvalidQr()
    {
        Mock<IReceiptRepository> receiptRepository = new(MockBehavior.Strict);
        Mock<IQrParser> qrParser = new(MockBehavior.Strict);
        qrParser.Setup(parser => parser.Parse("bad")).Returns((QrParsed)null!);
        SaveReceiptsScannedUseCase useCase = new(CreateUnitOfWork(receiptRepository, new Mock<IReceiptAccountRepository>(MockBehavior.Strict)).Object, CreateAccessUseCase().Object, qrParser.Object);

        ServiceResult<AddReceiptScanResponse> result = await useCase.ExecuteAsync(new QrScanRequest
        {
            Results = [new QrScanResult { DecodedText = "bad" }]
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data?.ScannedCount);
        Assert.Equal(0, result.Data?.AddedToDbCount);
        Assert.Equal(1, result.Data?.ErrorCount);
        Assert.NotNull(result.Data?.Results.Single().ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_AddsExistingReceiptToAccount_WhenReceiptExistsForUserButNotAccount()
    {
        Guid accountId = Guid.NewGuid();
        Receipt existingReceipt = TestReceiptFactory.Create(Guid.NewGuid());
        Mock<IReceiptRepository> receiptRepository = CreateReceiptRepositoryForSequence(null, existingReceipt);
        Mock<IReceiptAccountRepository> receiptAccountRepository = new(MockBehavior.Strict);
        receiptAccountRepository.Setup(repository => repository.Create(It.IsAny<ReceiptAccount>()));
        Mock<IQrParser> qrParser = new(MockBehavior.Strict);
        qrParser.Setup(parser => parser.Parse("qr")).Returns(CreateParsed());
        SaveReceiptsScannedUseCase useCase = new(CreateUnitOfWork(receiptRepository, receiptAccountRepository).Object, CreateAccessUseCase().Object, qrParser.Object);

        ServiceResult<AddReceiptScanResponse> result = await useCase.ExecuteAsync(new QrScanRequest
        {
            AccountId = accountId,
            Results = [new QrScanResult { DecodedText = "qr" }]
        }, Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data?.AddedToDbCount);
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

    private static Mock<IValidateReceiptCreationAccessUseCase> CreateAccessUseCase()
    {
        Mock<IValidateReceiptCreationAccessUseCase> useCase = new(MockBehavior.Strict);
        useCase.Setup(item => item.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
        return useCase;
    }
}
