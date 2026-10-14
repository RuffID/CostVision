using CostVision.Tests;
using System.Linq.Expressions;
using System.Security.Cryptography;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Infrastructure.Services.Helpers;
using Moq;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.Services.Helpers;

public class AuthenticationPasswordCompatibilityTests
{
    [Theory]
    [InlineData("Password1!")]
    [InlineData(" Password1! ")]
    public async Task ExistingPbkdf2Hash_IsAcceptedWithoutRewriting(string password)
    {
        // Arrange
        byte[] salt = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
        byte[] key = Rfc2898DeriveBytes.Pbkdf2("Password1!", salt, 100000, HashAlgorithmName.SHA256, 32);
        string storedHash = $"{Convert.ToHexString(key)}:{Convert.ToHexString(salt)}:100000:SHA256";
        Fixture data = new(storedHash);
        AuthenticateUserUseCase login = TestUseCaseFactory.CreateAuthenticateUserUseCase(data.Work.Object, new Hasher());

        // Act
        var result = await login.ExecuteAsync(new() { Login = " USER ", Password = password }, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(data.User.Id, result.Data?.Id);
        Assert.Equal(storedHash, data.User.PasswordHash);
        data.Users.Verify(repository => repository.Create(It.IsAny<User>()), Times.Never);
        data.Work.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateChangeAndEmptyPasswordUpdate_UseSameLoginAndPasswordRules()
    {
        // Arrange
        Fixture data = new("existing-hash");
        Hasher hasher = new();
        data.Items.Clear();
        CreateUserUseCase create = TestUseCaseFactory.CreateCreateUserUseCase(data.Work.Object, hasher);
        UserUpsertRequest request = new()
        {
            Login = " NewUser ", Name = " New User ", Password = " Password1! ", RoleIds = [data.Role.Id]
        };

        // Act
        ServiceResult created = await create.ExecuteAsync(request, CancellationToken.None);

        // Assert
        Assert.True(created.Success);
        User user = Assert.Single(data.Items);
        Assert.Equal("NewUser", user.Login);
        Assert.True(hasher.Verify("Password1!", user.PasswordHash));

        // Act
        var firstLogin = await TestUseCaseFactory.CreateAuthenticateUserUseCase(data.Work.Object, hasher)
            .ExecuteAsync(new() { Login = " NEWUSER ", Password = " Password1! " }, CancellationToken.None);

        // Assert
        Assert.True(firstLogin.Success);
        string initialHash = user.PasswordHash;

        // Arrange
        request.Id = user.Id;
        request.Password = " Changed1! ";

        // Act
        ServiceResult changed = await TestUseCaseFactory.CreateUpdateUserUseCase(data.Work.Object, hasher).ExecuteAsync(request, CancellationToken.None);
        var changedLogin = await TestUseCaseFactory.CreateAuthenticateUserUseCase(data.Work.Object, hasher)
            .ExecuteAsync(new() { Login = "newuser", Password = " Changed1! " }, CancellationToken.None);

        // Assert
        Assert.True(changed.Success);
        Assert.True(changedLogin.Success);
        Assert.NotEqual(initialHash, user.PasswordHash);
        Assert.False(hasher.Verify("Password1!", user.PasswordHash));
        Assert.True(hasher.Verify("Changed1!", user.PasswordHash));
        string changedHash = user.PasswordHash;

        // Arrange
        request.Password = " ";

        // Act
        ServiceResult emptyUpdate = await TestUseCaseFactory.CreateUpdateUserUseCase(data.Work.Object, hasher).ExecuteAsync(request, CancellationToken.None);

        // Assert
        Assert.True(emptyUpdate.Success);
        Assert.Equal(changedHash, user.PasswordHash);
    }

    private class Fixture
    {
        public Mock<ITestStorage> Work { get; } = new(MockBehavior.Strict);
        public Mock<IUserRepository> Users { get; } = new(MockBehavior.Strict);
        public List<User> Items { get; } = [];
        public Role Role { get; }
        public User User { get; }

        public Fixture(string hash)
        {
            Assert.True(Role.TryCreate("User", RoleType.User, out Role? role, out _));
            Role = role!;
            Role.Id = Guid.NewGuid();
            Assert.True(User.TryCreate("user", "User", hash, [Role], DateTime.UtcNow, out User? user, out _));
            User = user!;
            User.Id = Guid.NewGuid();
            Items.Add(User);

            Users.Setup(repository => repository.GetItemByPredicateAsync(It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<bool>(), It.IsAny<Func<IQueryable<User>, IQueryable<User>>>(), It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<User, bool>> predicate, bool tracking,
                    Func<IQueryable<User>, IQueryable<User>> include, CancellationToken ct) =>
                    Task.FromResult(Items.FirstOrDefault(predicate.Compile())));
            Users.Setup(repository => repository.Create(It.IsAny<User>())).Callback((User item) =>
            {
                item.Id = Guid.NewGuid();
                Items.Add(item);
            });

            Mock<IRoleRepository> roles = new(MockBehavior.Strict);
            roles.Setup(repository => repository.GetItemsByPredicateAsync(It.IsAny<Expression<Func<Role, bool>>>(),
                It.IsAny<int>(), It.IsAny<int?>(), true, null, It.IsAny<CancellationToken>()))
                .Returns((Expression<Func<Role, bool>> predicate, int skip, int? take, bool tracking,
                    Func<IQueryable<Role>, IQueryable<Role>> include, CancellationToken ct) =>
                    Task.FromResult(new[] { Role }.Where(predicate.Compile()).ToList()));

            Work.Setup(work => work.User).Returns(Users.Object);
            Work.Setup(work => work.Role).Returns(roles.Object);
            Work.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        }
    }
}
