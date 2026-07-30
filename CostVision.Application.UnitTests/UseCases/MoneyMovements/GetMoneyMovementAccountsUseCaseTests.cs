using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class GetMoneyMovementAccountsUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task GetMoneyMovementAccounts_ReturnsAccessibleAccountsSortedByName()
    {
        Guid userId = Guid.NewGuid();
        Account bAccount = CreateAccount(Guid.NewGuid(), Guid.NewGuid(), "B");
        bAccount.TryAddMember(userId, AccountAccessRole.Viewer, out _, out _);
        Account aAccount = CreateAccount(Guid.NewGuid(), userId, "A");
        aAccount.CreatedByUser = new User { Id = userId, Name = "Owner" };
        Mock<IAccountRepository> accountRepository = CreateAccountRepository();
        accountRepository
            .Setup(repository => repository.GetItemsByPredicateAsync(
                It.IsAny<Expression<Func<Account, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int?>(),
                true,
                It.IsAny<Func<IQueryable<Account>, IQueryable<Account>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([bAccount, aAccount]);
        GetMoneyMovementAccountsUseCase useCase = new(CreateUnitOfWork(accountRepository: accountRepository).Object);

        var result = await useCase.ExecuteAsync(userId, CancellationToken.None);

        Assert.Equal(["A", "B"], result.Select(item => item.Name).ToList());
        Assert.True(result[0].CanManage);
        Assert.Equal(AccountAccessRole.Viewer, result[1].AccessRole);
    }
}
