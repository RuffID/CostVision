using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Products;

public class GetProductListUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsPagedProductsWithAdaptiveDisplayNameAndAccessibleReceiptCount()
    {
        Guid currentUserId = Guid.NewGuid();
        Product milk = CreateProduct("Milk", "Milk 1L", currentUserId, accessibleReceiptCount: 2);
        Product bread = CreateProduct("Bread", null, currentUserId, accessibleReceiptCount: 1);
        List<Product> products = [milk, bread];

        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.CountByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(products.Count);
        productRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Product).Returns(productRepository.Object);
        GetProductListUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new GetProductListRequest
        {
            UseAdaptiveNames = true,
            Page = 1,
            PageSize = 1,
            SortBy = "receiptCount",
            SortDirection = "desc"
        }, currentUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("Milk 1L", result.Data.Items[0].DisplayName);
        Assert.Equal(2, result.Data.Items[0].ReceiptCount);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.TotalPages);
        Assert.False(result.Data.HasPreviousPage);
        Assert.True(result.Data.HasNextPage);
    }

    [Fact]
    public async Task ExecuteAsync_ClampsPageAndPageSize_WhenRequestValuesAreOutOfRange()
    {
        Guid currentUserId = Guid.NewGuid();
        List<Product> products = [CreateProduct("Bread", null, currentUserId, accessibleReceiptCount: 1)];

        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.CountByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(products.Count);
        productRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(products);

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Product).Returns(productRepository.Object);
        GetProductListUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new GetProductListRequest
        {
            Page = 0,
            PageSize = 500
        }, currentUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.Page);
        Assert.Equal(100, result.Data.PageSize);
        Assert.Single(result.Data.Items);
    }

    private static Product CreateProduct(string name, string? adaptiveName, Guid currentUserId, int accessibleReceiptCount)
    {
        Product product = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            AdaptiveName = adaptiveName
        };

        for (int index = 0; index < accessibleReceiptCount; index++)
        {
            Receipt receipt = new()
            {
                Id = Guid.NewGuid(),
                CreatedByUserId = currentUserId
            };

            product.ReceiptItems.Add(new ReceiptItem
            {
                Id = Guid.NewGuid(),
                ReceiptId = receipt.Id,
                Receipt = receipt,
                Product = product
            });
        }

        Receipt inaccessibleReceipt = new()
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };
        product.ReceiptItems.Add(new ReceiptItem
        {
            Id = Guid.NewGuid(),
            ReceiptId = inaccessibleReceipt.Id,
            Receipt = inaccessibleReceipt,
            Product = product
        });

        return product;
    }
}
