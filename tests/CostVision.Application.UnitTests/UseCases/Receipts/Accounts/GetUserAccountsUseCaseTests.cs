using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Application.Models.Requests.Receipts;
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

        ServiceResult<List<UserAccountViewModel>> result = await useCase.ExecuteAsync(userId, includeArchived: false, CancellationToken.None);

        Assert.True(result.Success);
        UserAccountViewModel accountResult = Assert.Single(result.Data);
        Assert.Equal(accountId, accountResult.Id);
        Assert.Equal("Shared", accountResult.Name);
        Assert.Equal("Groceries", accountResult.Description);
        Assert.Equal("#ABCDEF", accountResult.ColorHex);
        Assert.True(accountResult.IsActive);
        Assert.False(accountResult.CanManage);
        Assert.Equal("Owner", accountResult.OwnerName);
        Assert.Equal(AccountAccessRole.Editor, accountResult.AccessRole);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsEmptyList_WhenUserHasNoMemberships()
    {
        GetUserAccountsUseCase useCase = new(CreateUnitOfWork([], []).Object);

        ServiceResult<List<UserAccountViewModel>> result = await useCase.ExecuteAsync(Guid.NewGuid(), includeArchived: false, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Data);
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
