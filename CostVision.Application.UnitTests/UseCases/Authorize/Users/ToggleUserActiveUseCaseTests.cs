using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Domain.Models.Authorization;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Users;

public class ToggleUserActiveUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DeactivatesActiveUser()
    {
        Guid userId = Guid.NewGuid();
        User user = TestUserFactory.Create(userId);

        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByIdAsync(userId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        ToggleUserActiveUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Data);
        Assert.False(user.IsActive);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ActivatesInactiveUser()
    {
        Guid userId = Guid.NewGuid();
        User user = TestUserFactory.Create(userId, isActive: false);

        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByIdAsync(userId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        ToggleUserActiveUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object);

        var result = await useCase.ExecuteAsync(userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenUserDoesNotExist()
    {
        Guid userId = Guid.NewGuid();
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByIdAsync(userId, It.IsAny<bool>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        ToggleUserActiveUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object);

        var result = await useCase.ExecuteAsync(userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }
}
