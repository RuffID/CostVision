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
        Guid productId = Guid.NewGuid();
        Product product = new() { Id = productId, Name = "Milk" };

        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.GetItemByIdAsync(productId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(productRepository);
        UpdateProductAdaptiveNameUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = productId,
            AdaptiveName = "  Milk 1L  "
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Milk 1L", product.AdaptiveName);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ClearsAdaptiveName_WhenAdaptiveNameIsWhitespace()
    {
        Guid productId = Guid.NewGuid();
        Product product = new() { Id = productId, Name = "Milk", AdaptiveName = "Milk 1L" };

        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        productRepository
            .Setup(repository => repository.GetItemByIdAsync(productId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        UpdateProductAdaptiveNameUseCase useCase = new(CreateUnitOfWork(productRepository).Object);

        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = productId,
            AdaptiveName = " "
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(product.AdaptiveName);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenProductDoesNotExist()
    {
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
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenProductIdIsEmpty()
    {
        Mock<IProductRepository> productRepository = new(MockBehavior.Strict);
        UpdateProductAdaptiveNameUseCase useCase = new(CreateUnitOfWork(productRepository).Object);

        var result = await useCase.ExecuteAsync(new UpdateProductAdaptiveNameRequest
        {
            ProductId = Guid.Empty,
            AdaptiveName = "Milk"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IProductRepository> productRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Product).Returns(productRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return unitOfWork;
    }
}
