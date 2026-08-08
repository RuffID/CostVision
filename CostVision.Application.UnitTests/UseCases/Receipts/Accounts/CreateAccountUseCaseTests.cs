using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class CreateAccountUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CreatesAccountAndOwnerMember_WhenRequestIsValid()
    {
        Guid ownerUserId = Guid.NewGuid();
        Account? createdAccount = null;
        AccountMember? createdMember = null;

        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        accountRepository.Setup(repository => repository.Create(It.IsAny<Account>()))
            .Callback<Account>(account => createdAccount = account);

        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        accountMemberRepository.Setup(repository => repository.Create(It.IsAny<AccountMember>()))
            .Callback<AccountMember>(member => createdMember = member);

        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(accountRepository, accountMemberRepository);
        CreateAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(ownerUserId, new CreateAccountRequest
        {
            Name = "Food",
            Description = "Shared groceries",
            ColorHex = "#abcdef"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Food", result.Data?.Name);
        Assert.Equal(AccountAccessRole.Owner, result.Data?.AccessRole);
        Assert.NotNull(createdAccount);
        Assert.Equal("Food", createdAccount.Name);
        Assert.Equal("Shared groceries", createdAccount.Description);
        Assert.Equal("#ABCDEF", createdAccount.ColorHex);
        Assert.Equal(ownerUserId, createdAccount.CreatedByUserId);
        Assert.NotEqual(default, createdAccount.CreatedAtUtc);
        Assert.NotNull(createdMember);
        Assert.Equal(ownerUserId, createdMember.UserId);
        Assert.Same(createdAccount, createdMember.Account);
        Assert.Equal(AccountAccessRole.Owner, createdMember.Role);
        unitOfWork.Verify(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenColorIsInvalid()
    {
        Mock<IAccountRepository> accountRepository = new(MockBehavior.Strict);
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        CreateAccountUseCase useCase = new(CreateUnitOfWork(accountRepository, accountMemberRepository).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), new CreateAccountRequest
        {
            Name = "Food",
            ColorHex = "ABCDEF"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(Mock<IAccountRepository> accountRepository, Mock<IAccountMemberRepository> accountMemberRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.Account).Returns(accountRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.AccountMember).Returns(accountMemberRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, CancellationToken>((action, _) => action());

        return unitOfWork;
    }
}
