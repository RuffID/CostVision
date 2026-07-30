using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class GetUserAccountsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyUserAccountsProjectedToDto()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        User owner = TestUserFactory.Create(name: "Owner");
        Account account = TestAccountFactory.Create(
            accountId,
            name: "Shared",
            description: "Groceries",
            colorHex: "#ABCDEF",
            owner: owner);
        account.TryAddMember(userId, AccountAccessRole.Editor, out AccountMember? membership, out _);
        Assert.NotNull(membership);
        List<AccountMember> memberships = [membership];
        List<Account> accounts = [account];
        GetUserAccountsUseCase useCase = new(CreateUnitOfWork(memberships, accounts).Object);

        var result = await useCase.ExecuteAsync(userId, includeArchived: false, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(accountId, result[0].Id);
        Assert.Equal("Shared", result[0].Name);
        Assert.Equal("Groceries", result[0].Description);
        Assert.Equal("#ABCDEF", result[0].ColorHex);
        Assert.True(result[0].IsActive);
        Assert.False(result[0].CanManage);
        Assert.Equal("Owner", result[0].OwnerName);
        Assert.Equal(AccountAccessRole.Editor, result[0].AccessRole);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyList_WhenUserHasNoMemberships()
    {
        GetUserAccountsUseCase useCase = new(CreateUnitOfWork([], []).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), includeArchived: false, CancellationToken.None);

        Assert.Empty(result);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(List<AccountMember> memberships, List<Account> accounts)
    {
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        accountMemberRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<AccountMember, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberships);

        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        if (memberships.Count > 0)
        {
            accountRepository
                .Setup(repository => repository.GetItemsByPredicateAsync(
                    It.IsAny<Expression<Func<Account, bool>>>(),
                    It.IsAny<int>(),
                    It.IsAny<int?>(),
                    true,
                    It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(accounts);
        }

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.AccountMember).Returns(accountMemberRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        return unitOfWork;
    }
}
