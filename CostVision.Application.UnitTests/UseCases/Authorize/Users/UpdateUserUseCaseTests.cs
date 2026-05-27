using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Domain.Models.Authorization;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Users;

public class UpdateUserUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_UpdatesUserAndRoles_WhenRequestIsValid()
    {
        Guid userId = Guid.NewGuid();
        Guid oldRoleId = Guid.NewGuid();
        Guid newRoleId = Guid.NewGuid();
        User user = new()
        {
            Id = userId,
            Login = "old",
            Name = "Old Name",
            PasswordHash = "old-hash",
            UserRoles = new List<UserRole> { new() { UserId = userId, RoleId = oldRoleId } }
        };

        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .SetupSequence(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user)
            .ReturnsAsync((User?)null);

        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        passwordHasher.Setup(hasher => hasher.Hash("new-password")).Returns("new-hash");

        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        UpdateUserUseCase useCase = new(unitOfWork.Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Id = userId,
            Login = "new-login",
            Name = "New Name",
            Password = " new-password ",
            RoleIds = [newRoleId]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("new-login", user.Login);
        Assert.Equal("New Name", user.Name);
        Assert.Equal("new-hash", user.PasswordHash);
        Assert.DoesNotContain(user.UserRoles, userRole => userRole.RoleId == oldRoleId);
        Assert.Contains(user.UserRoles, userRole => userRole.RoleId == newRoleId);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenUserDoesNotExist()
    {
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        UpdateUserUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Id = Guid.NewGuid(),
            Login = "user",
            Name = "Test User",
            RoleIds = [Guid.NewGuid()]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenIdIsMissing()
    {
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        UpdateUserUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Login = "user",
            Name = "Test User",
            RoleIds = [Guid.NewGuid()]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }
}
