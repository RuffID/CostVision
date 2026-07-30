using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class UpdateAccountMembersUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_AddsRemovesAndUpdatesMembers_WhenRequestIsValid()
    {
        Guid ownerUserId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid removedUserId = Guid.NewGuid();
        Guid updatedUserId = Guid.NewGuid();
        Guid addedUserId = Guid.NewGuid();
        Account.TryCreate("Основной", null, null, ownerUserId, DateTime.UtcNow, out Account? account, out string? creationError);
        Assert.NotNull(account);
        Assert.Null(creationError);
        account.Id = accountId;
        account.TryAddMember(removedUserId, AccountAccessRole.Editor, out _, out _);
        account.TryAddMember(updatedUserId, AccountAccessRole.Viewer, out _, out _);
        List<AccountMember>? removedMembers = null;
        AccountMember? addedMember = null;

        Mock<IAccountRepository> accountRepository = CreateAccountRepository(account);
        Mock<IUserRepository> userRepository = CreateUsersRepository([
            TestUserFactory.Create(updatedUserId),
            TestUserFactory.Create(addedUserId)
        ]);
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        accountMemberRepository.Setup(repository => repository.DeleteRange(It.IsAny<IEnumerable<AccountMember>>()))
            .Callback<IEnumerable<AccountMember>>(members => removedMembers = members.ToList());
        accountMemberRepository.Setup(repository => repository.CreateRange(It.IsAny<IEnumerable<AccountMember>>()))
            .Callback<IEnumerable<AccountMember>>(members => addedMember = members.Single());
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(accountRepository, userRepository, accountMemberRepository);
        UpdateAccountMembersUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(accountId, ownerUserId,
        [
            new UpdateAccountMemberRequest { UserId = updatedUserId, Role = AccountAccessRole.Editor },
            new UpdateAccountMemberRequest { UserId = addedUserId, Role = AccountAccessRole.Viewer }
        ], CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(removedMembers);
        Assert.Contains(removedMembers, member => member.UserId == removedUserId);
        Assert.Equal(AccountAccessRole.Editor, account.Members.Single(member => member.UserId == updatedUserId).Role);
        Assert.NotNull(addedMember);
        Assert.Equal(accountId, addedMember.AccountId);
        Assert.Equal(addedUserId, addedMember.UserId);
        Assert.Equal(AccountAccessRole.Viewer, addedMember.Role);
        unitOfWork.Verify(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenOwnerRoleIsAssignedToMember()
    {
        Guid ownerUserId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Account account = TestAccountFactory.Create(accountId, ownerUserId);
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        UpdateAccountMembersUseCase useCase = new(CreateUnitOfWork(CreateAccountRepository(account), userRepository, accountMemberRepository).Object);

        var result = await useCase.ExecuteAsync(accountId, ownerUserId,
        [
            new UpdateAccountMemberRequest { UserId = Guid.NewGuid(), Role = AccountAccessRole.Owner }
        ], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenSelectedUserDoesNotExistOrInactive()
    {
        Guid ownerUserId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Account account = TestAccountFactory.Create(accountId, ownerUserId);
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        UpdateAccountMembersUseCase useCase = new(CreateUnitOfWork(CreateAccountRepository(account), CreateUsersRepository([]), accountMemberRepository).Object);

        var result = await useCase.ExecuteAsync(accountId, ownerUserId,
        [
            new UpdateAccountMemberRequest { UserId = Guid.NewGuid(), Role = AccountAccessRole.Viewer }
        ], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenAccountDoesNotBelongToOwner()
    {
        Mock<IAccountRepository> accountRepository = CreateAccountRepository(null);
        Mock<IUserRepository> userRepository = new(MockBehavior.Strict);
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        UpdateAccountMembersUseCase useCase = new(CreateUnitOfWork(accountRepository, userRepository, accountMemberRepository).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), [], CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    private static Mock<IAccountRepository> CreateAccountRepository(Account? account)
    {
        Mock<IAccountRepository> repository = new(MockBehavior.Strict);
        repository
            .Setup(item => item.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<Account, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return repository;
    }

    private static Mock<IUserRepository> CreateUsersRepository(List<User> users)
    {
        Mock<IUserRepository> repository = new(MockBehavior.Strict);
        repository
            .Setup(item => item.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);
        return repository;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IAccountRepository> accountRepository, Mock<IUserRepository> userRepository, Mock<IAccountMemberRepository> accountMemberRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.User).Returns(userRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.AccountMember).Returns(accountMemberRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((action, _) => action());
        return unitOfWork;
    }
}
