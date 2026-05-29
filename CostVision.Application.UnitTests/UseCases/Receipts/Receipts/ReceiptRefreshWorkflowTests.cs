using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.Service.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Receipts.Refresh;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Receipts;

public class ReceiptRefreshWorkflowTests
{
    [Fact]
    public async Task RefreshAsync_AppliesExternalReceiptRecreatesItemsAndCreatesMissingProduct()
    {
        Receipt receipt = new()
        {
            Id = Guid.NewGuid(),
            RetailPlace = "Old",
            Items = [new ReceiptItem { Id = Guid.NewGuid() }]
        };
        Product sourceProduct = new() { Name = "Milk", NormalizedName = "milk" };
        Receipt externalReceipt = new()
        {
            RetailPlace = "New",
            Items = [new ReceiptItem { Product = sourceProduct, Price = 10, Quantity = 2, Sum = 20 }]
        };
        Product? createdProduct = null;
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
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(productRepository);
        ReceiptRefreshWorkflow workflow = new(unitOfWork.Object, externalReceiptProvider.Object);

        var result = await workflow.RefreshAsync(receipt, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New", receipt.RetailPlace);
        Assert.NotNull(receipt.UpdatedAtUtc);
        Assert.Single(receipt.Items);
        Assert.Equal(20, receipt.Items.Single().Sum);
        Assert.NotNull(createdProduct);
        Assert.Equal("Milk", createdProduct.Name);
        Assert.Same(createdProduct, receipt.Items.Single().Product);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_ReturnsProviderError_WhenExternalProviderFails()
    {
        Receipt receipt = new() { Id = Guid.NewGuid() };
        Mock<IExternalReceiptProvider> externalReceiptProvider = new(MockBehavior.Strict);
        externalReceiptProvider.Setup(provider => provider.GetReceiptAsync(receipt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Receipt>.Fail(502, "Provider error"));
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        ReceiptRefreshWorkflow workflow = new(CreateUnitOfWork(productRepository).Object, externalReceiptProvider.Object);

        var result = await workflow.RefreshAsync(receipt, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(502, result.Error?.StatusCode);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IProductRepository> productRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Product).Returns(productRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return unitOfWork;
    }
}
