using CostVision.Tests;
using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Products;

public class ProductAdditionalTests
{
    [Fact]
    public async Task GetProductListExecuteAsync_ReturnsEmptyPage_WhenNoProductsFound()
    {
        // Arrange
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        ProjectedReadTestFactory.Configure(productRepository, () => Array.Empty<Product>());
        productRepository.Setup(repository => repository.CountByPredicateAsync(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        productRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Product>, IQueryable<Product>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        GetProductListUseCase useCase = TestUseCaseFactory.CreateGetProductListUseCase(CreateUnitOfWork(productRepository).Object);

        // Act
        var result = await useCase.ExecuteAsync(new GetProductListRequest { Search = "milk" }, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalCount);
        Assert.Equal(1, result.Data.TotalPages);
    }

    [Fact]
    public async Task UpdateProductAdaptiveNameExecuteAsync_ReturnsBadRequest_WhenAdaptiveNameIsTooLong()
    {
        // Arrange
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        UpdateProductAdaptiveNameUseCase useCase = TestUseCaseFactory.CreateUpdateProductAdaptiveNameUseCase(CreateUnitOfWork(productRepository).Object);

        // Act
        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = Guid.NewGuid(),
            AdaptiveName = new string('a', 501)
        }, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    private static Mock<ITestStorage> CreateUnitOfWork(Mock<IProductRepository> productRepository)
    {
        Mock<ITestStorage> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Product).Returns(productRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return unitOfWork;
    }
}
