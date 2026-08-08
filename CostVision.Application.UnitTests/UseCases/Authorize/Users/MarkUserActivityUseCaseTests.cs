using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Domain.Models.Authorization;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Users;

public class MarkUserActivityUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesActivity_WhenUserExistsAndActive()
    {
        Guid userId = Guid.NewGuid();
        User user = TestUserFactory.Create(userId);
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByIdAsync(userId, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        MarkUserActivityUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.NotNull(user.LastLoginAtUtc);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenUserDoesNotExist()
    {
        Guid userId = Guid.NewGuid();
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByIdAsync(userId, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        MarkUserActivityUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object);

        var result = await useCase.ExecuteAsync(userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenUserIdIsEmpty()
    {
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        MarkUserActivityUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object);

        var result = await useCase.ExecuteAsync(Guid.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }
}
