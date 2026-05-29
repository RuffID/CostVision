using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Domain.Models.Authorization;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Authentication;

public class AuthenticateUserUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenLoginAndPasswordAreValid()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Login = "user",
            PasswordHash = "hash",
            IsActive = true
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
        passwordHasher.Setup(hasher => hasher.Verify("password", "hash")).Returns(true);

        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        AuthenticateUserUseCase useCase = new(unitOfWork.Object, passwordHasher.Object);

        DateTime beforeLoginAtUtc = DateTime.UtcNow;
        var result = await useCase.ExecuteAsync(new LoginRequest { Login = "user", Password = "password" }, CancellationToken.None);
        DateTime afterLoginAtUtc = DateTime.UtcNow;

        Assert.True(result.Success);
        Assert.Same(user, result.Data);
        Assert.NotNull(user.LastLoginAtUtc);
        Assert.InRange(user.LastLoginAtUtc.Value, beforeLoginAtUtc, afterLoginAtUtc);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUnauthorized_WhenPasswordIsInvalid()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Login = "user",
            PasswordHash = "hash",
            IsActive = true
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
        passwordHasher.Setup(hasher => hasher.Verify("bad", "hash")).Returns(false);

        AuthenticateUserUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new LoginRequest { Login = "user", Password = "bad" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenUserIsInactive()
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Login = "user",
            PasswordHash = "hash",
            IsActive = false
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
        passwordHasher.Setup(hasher => hasher.Verify("password", "hash")).Returns(true);

        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        AuthenticateUserUseCase useCase = new(unitOfWork.Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new LoginRequest { Login = "user", Password = "password" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Null(user.LastLoginAtUtc);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenCredentialsAreEmpty()
    {
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        AuthenticateUserUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new LoginRequest { Login = " ", Password = " " }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }
}
