using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.Receipts.Products;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Products;

public class UpdateProductAdaptiveNameUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_TrimsAndSavesAdaptiveName_WhenProductExists()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        Product product = TestReceiptFactory.CreateProduct("Milk");
        product.Id = productId;

        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.GetItemByIdAsync(productId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        productRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Product, bool>>>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(productRepository);
        UpdateProductAdaptiveNameUseCase useCase = new(unitOfWork.Object);

        // Act
        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = productId,
            AdaptiveName = "  Milk 1L  "
        }, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Milk 1L", product.AdaptiveName);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ClearsAdaptiveName_WhenAdaptiveNameIsWhitespace()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        Product product = TestReceiptFactory.CreateProduct("Milk", "Milk 1L");
        product.Id = productId;

        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.GetItemByIdAsync(productId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        UpdateProductAdaptiveNameUseCase useCase = new(CreateUnitOfWork(productRepository).Object);

        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = productId,
            AdaptiveName = " "
        }, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Null(product.AdaptiveName);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenProductDoesNotExist()
    {
        // Arrange
        Guid productId = Guid.NewGuid();
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.GetItemByIdAsync(productId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        UpdateProductAdaptiveNameUseCase useCase = new(CreateUnitOfWork(productRepository).Object);

        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = productId,
            AdaptiveName = "Milk"
        }, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenProductIdIsEmpty()
    {
        // Arrange
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        UpdateProductAdaptiveNameUseCase useCase = new(CreateUnitOfWork(productRepository).Object);

        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = Guid.Empty,
            AdaptiveName = "Milk"
        }, Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IProductRepository> productRepository)
    {
        Mock<IReceiptRepository> receipts = new(MockBehavior.Strict);
        receipts.Setup(repository => repository.GetItemByPredicateAsync(
            It.IsAny<Expression<Func<Receipt, bool>>>(), true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestReceiptFactory.Create());

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(work => work.Receipt).Returns(receipts.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.Product).Returns(productRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return unitOfWork;
    }
}
