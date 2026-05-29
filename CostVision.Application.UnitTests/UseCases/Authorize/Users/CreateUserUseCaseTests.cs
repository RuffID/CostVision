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

public class CreateUserUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesActiveUserWithHashedPasswordAndRoles_WhenRequestIsValid()
    {
        Guid roleId = Guid.NewGuid();
        User? createdUser = null;

        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        userRepository.Setup(repository => repository.Create(It.IsAny<User>()))
            .Callback<User>(user => createdUser = user);

        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        passwordHasher.Setup(hasher => hasher.Hash("Password1!")).Returns("hashed-password");

        Mock<IUnitOfWork> unitOfWork = TestUnitOfWorkFactory.CreateWithUserRepository(userRepository);
        CreateUserUseCase useCase = new(unitOfWork.Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Login = " user ",
            Name = " Test User ",
            Password = " Password1! ",
            RoleIds = [roleId]
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(createdUser);
        Assert.Equal("user", createdUser.Login);
        Assert.Equal("Test User", createdUser.Name);
        Assert.Equal("hashed-password", createdUser.PasswordHash);
        Assert.True(createdUser.IsActive);
        Assert.Contains(createdUser.UserRoles, userRole => userRole.RoleId == roleId);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenLoginAlreadyExists()
    {
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = Guid.NewGuid(), Login = "user" });

        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        CreateUserUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Login = "user",
            Name = "Test User",
            Password = "Password1!",
            RoleIds = [Guid.NewGuid()]
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenRoleIsMissing()
    {
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        Mock<IPasswordHasher> passwordHasher = new(MockBehavior.Strict);
        CreateUserUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new UserUpsertRequest
        {
            Login = "user",
            Name = "Test User",
            Password = "Password1!",
            RoleIds = []
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }
}
