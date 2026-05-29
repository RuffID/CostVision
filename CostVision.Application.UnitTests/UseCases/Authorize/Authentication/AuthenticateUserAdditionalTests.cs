using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.Service.Authorize;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Domain.Models.Authorization;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Authentication;

public class AuthenticateUserAdditionalTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsUnauthorized_WhenUserDoesNotExist()
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
        AuthenticateUserUseCase useCase = new(TestUnitOfWorkFactory.CreateWithUserRepository(userRepository).Object, passwordHasher.Object);

        var result = await useCase.ExecuteAsync(new LoginRequest { Login = "missing", Password = "password" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(401, result.Error?.StatusCode);
    }
}
