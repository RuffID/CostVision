using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class GetAccountShareUsersUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsActiveUsersAndMarksSelectedMembers()
    {
        Guid ownerUserId = Guid.NewGuid();
        Guid selectedUserId = Guid.NewGuid();
        Guid availableUserId = Guid.NewGuid();
        Account account = new()
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = ownerUserId,
            Members = [new AccountMember { UserId = selectedUserId, Role = AccountAccessRole.Editor }]
        };
        List<User> users =
        [
            new User { Id = selectedUserId, Name = "Beta", Login = "beta", IsActive = true },
            new User { Id = availableUserId, Name = "Alpha", Login = "alpha", IsActive = true }
        ];
        GetAccountShareUsersUseCase useCase = new(CreateUnitOfWork(account, users).Object);

        var result = await useCase.ExecuteAsync(account.Id, ownerUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(["Alpha", "Beta"], result.Data.Select(item => item.Name).ToList());
        Assert.False(result.Data.Single(item => item.Id == availableUserId).IsSelected);
        Assert.True(result.Data.Single(item => item.Id == selectedUserId).IsSelected);
        Assert.Equal(AccountAccessRole.Editor, result.Data.Single(item => item.Id == selectedUserId).Role);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenAccountDoesNotBelongToOwner()
    {
        GetAccountShareUsersUseCase useCase = new(CreateUnitOfWork(null, []).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Account? account, List<User> users)
    {
        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        accountRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Account, bool>>>(),
                true,
                It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        if (account != null)
        {
            userRepository
                .Setup(repository => repository.GetItemsByPredicateAsync(
                    It.IsAny<Expression<Func<User, bool>>>(),
                    It.IsAny<int>(),
                    It.IsAny<int?>(),
                    true,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);
        }

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.User).Returns(userRepository.Object);
        return unitOfWork;
    }
}
