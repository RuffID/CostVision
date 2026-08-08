using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Enums.Receipts;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class ReceiptRefreshWorkflowTests
{
    [Fact]
    public async Task RefreshAsync_AppliesExternalReceiptRecreatesItemsAndCreatesMissingProduct()
    {
        Store.TryCreate("Old", "OLD", "Old address", "OLDADDRESS", out Store? oldStore, out string? oldStoreError);
        Assert.NotNull(oldStore);
        Assert.Null(oldStoreError);
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid(), store: oldStore);
        Product oldProduct = TestReceiptFactory.CreateProduct("Old");
        Assert.True(ReceiptItem.TryCreate(1, 1, 1, 0, default, default, default, oldProduct, null, out ReceiptItem? oldItem, out string? oldItemError), oldItemError);
        Assert.True(receipt.TryAddItem(oldItem!, out oldItemError), oldItemError);
        Product sourceProduct = TestReceiptFactory.CreateProduct("Milk");
        Store.TryCreate("New", "NEW", "Address", "ADDRESS", out Store? sourceStore, out string? sourceStoreError);
        Assert.NotNull(sourceStore);
        Assert.Null(sourceStoreError);
        Assert.True(Receipt.TryCreate("fn", "fd", "fp", new DateTime(2026, 1, 1), ReceiptOperationType.Income, 20, Guid.NewGuid(), new DateTime(2026, 1, 1), out Receipt? externalReceipt, out string? receiptError), receiptError);
        externalReceipt!.AssignStore(sourceStore);
        Assert.True(ReceiptItem.TryCreate(10, 2, 20, 0, default, default, default, sourceProduct, null, out ReceiptItem? sourceItem, out string? itemError), itemError);
        Assert.True(externalReceipt.TryAddItem(sourceItem!, out itemError), itemError);
        Product? createdProduct = null;
        Store? createdStore = null;
        Mock<IExternalReceiptProvider> externalReceiptProvider = new(MockBehavior.Strict);
        externalReceiptProvider.Setup(provider => provider.GetReceiptAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Ok(externalReceipt));
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);
        productRepository.Setup(repository => repository.Create(It.IsAny<Product>()))
            .Callback<Product>(product => createdProduct = product);
        Mock<IStoreRepository> storeRepository = new(MockBehavior.Strict);
        storeRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Store, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<Store>, IQueryable<Store>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Store?)null);
        storeRepository.Setup(repository => repository.Create(It.IsAny<Store>()))
            .Callback<Store>(store => createdStore = store);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(productRepository, storeRepository);
        ReceiptRefreshWorkflow workflow = new(unitOfWork.Object, externalReceiptProvider.Object, TimeProvider.System);

        var result = await workflow.RefreshAsync(receipt, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New", receipt.Store?.Name);
        Assert.NotNull(createdStore);
        Assert.NotNull(receipt.UpdatedAtUtc);
        Assert.Equal(0, receipt.RefreshAttemptCount);
        Assert.Single(receipt.Items);
        Assert.Equal(20, receipt.Items.Single().Sum);
        Assert.NotNull(createdProduct);
        Assert.Equal("Milk", createdProduct.Name);
        Assert.Same(createdProduct, receipt.Items.Single().Product);
        Assert.Equal(ReceiptRefreshStatus.Completed, receipt.RefreshStatus);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsProviderError_WhenExternalProviderFails()
    {
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid());
        Mock<IExternalReceiptProvider> externalReceiptProvider = new(MockBehavior.Strict);
        externalReceiptProvider.Setup(provider => provider.GetReceiptAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "Provider error"));
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        ReceiptRefreshWorkflow workflow = new(CreateUnitOfWork(productRepository, null).Object, externalReceiptProvider.Object, TimeProvider.System);

        var result = await workflow.RefreshAsync(receipt, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.ExternalService, result.Error?.Type);
    }

    [Fact]
    public async Task RefreshScheduledAsync_MarksReceiptFailedAfterSeventhAttempt()
    {
        DateTime firstAttemptAtUtc = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        Receipt receipt = TestReceiptFactory.Create(Guid.NewGuid());
        Mock<IExternalReceiptProvider> externalReceiptProvider = new(MockBehavior.Strict);
        externalReceiptProvider
            .Setup(provider => provider.GetReceiptAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Fail(ServiceErrorType.ExternalService, "Provider error"));
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(new Mock<IProductRepository>(MockBehavior.Strict), null);
        ReceiptRefreshWorkflow workflow = new(unitOfWork.Object, externalReceiptProvider.Object, TimeProvider.System);

        for (int attemptIndex = 0; attemptIndex < ReceiptRefreshPolicy.MAX_ATTEMPTS; attemptIndex++)
        {
            DateTime attemptedAtUtc = firstAttemptAtUtc.AddDays(attemptIndex);
            ServiceResult<Receipt> result = await workflow.RefreshScheduledAsync(
                receipt,
                attemptedAtUtc,
                attemptedAtUtc.AddDays(1),
                CancellationToken.None);

            Assert.False(result.Success);
        }

        Assert.Equal(ReceiptRefreshPolicy.MAX_ATTEMPTS, receipt.RefreshAttemptCount);
        Assert.Equal(ReceiptRefreshStatus.Failed, receipt.RefreshStatus);
        Assert.Null(receipt.NextRefreshAttemptAtUtc);
        Assert.Equal("Provider error", receipt.LastRefreshError);
        Assert.False(receipt.CanAttemptRefresh(firstAttemptAtUtc.AddDays(7), ReceiptRefreshPolicy.MAX_ATTEMPTS));
        unitOfWork.Verify(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(ReceiptRefreshPolicy.MAX_ATTEMPTS));
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IProductRepository> productRepository, Mock<IStoreRepository>? storeRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Product).Returns(productRepository.Object);
        if (storeRepository != null)
            unitOfWork.Setup(unitOfWork => unitOfWork.Store).Returns(storeRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return unitOfWork;
    }
}
