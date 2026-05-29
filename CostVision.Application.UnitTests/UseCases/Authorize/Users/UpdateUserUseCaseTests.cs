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
        passwordHasher.Setup(hasher => hasher.Hash("NewPassword1!")).Returns("new-hash");

        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        UpdateUserUseCase useCase = new(unitOfWork.Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Id = userId,
            Login = "new-login",
            Name = "New Name",
            Password = " NewPassword1! ",
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
    public async Task ExecuteAsync_UpdatesUserWithoutChangingPasswordHash_WhenPasswordIsEmpty()
    {
        Guid userId = Guid.NewGuid();
        Guid roleId = Guid.NewGuid();
        User user = new()
        {
            Id = userId,
            Login = "user",
            Name = "Old Name",
            PasswordHash = "old-hash",
            UserRoles = new List<UserRole> { new() { UserId = userId, RoleId = roleId } }
        };

        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        UpdateUserUseCase useCase = new(unitOfWork.Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Id = userId,
            Login = "user",
            Name = "New Name",
            Password = " ",
            RoleIds = [roleId]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("New Name", user.Name);
        Assert.Equal("old-hash", user.PasswordHash);
        passwordHasher.Verify(hasher => hasher.Hash(It.IsAny<string>()), Times.Never);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenLoginAlreadyExists()
    {
        Guid userId = Guid.NewGuid();
        Guid roleId = Guid.NewGuid();
        User user = new()
        {
            Id = userId,
            Login = "old-login",
            Name = "Test User",
            PasswordHash = "old-hash",
            UserRoles = new List<UserRole> { new() { UserId = userId, RoleId = roleId } }
        };
        User conflictUser = new() { Id = Guid.NewGuid(), Login = "new-login" };

        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .SetupSequence(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Func<IQueryable<User>, IQueryable<User>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(user)
            .ReturnsAsync(conflictUser);

        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        UpdateUserUseCase useCase = new(unitOfWork.Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Id = userId,
            Login = "new-login",
            Name = "Test User",
            RoleIds = [roleId]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.Error?.StatusCode);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
