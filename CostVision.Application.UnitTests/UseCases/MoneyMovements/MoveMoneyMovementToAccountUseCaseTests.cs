using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class MoveMoneyMovementToAccountUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task MoveMoneyMovementToAccount_ChangesAccount_WhenBothAccountsAreEditable()
    {
        Guid userId = Guid.NewGuid();
        Guid sourceAccountId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();
        MoneyMovement movement = CreateMovement(Guid.NewGuid(), sourceAccountId, 100, DateTime.Today, userId);
        Mock<IAccountMemberRepository> accountMemberRepository = CreateAccountMemberRepositorySequence(
        [
            CreateMember(sourceAccountId, userId, AccountAccessRole.Editor),
            CreateMember(targetAccountId, userId, AccountAccessRole.Owner)
        ]);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(
            accountMemberRepository: accountMemberRepository,
            moneyMovementRepository: moneyMovementRepository,
            setupSaveChanges: true);
        MoveMoneyMovementToAccountUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new MoveMoneyMovementToAccountRequest
        {
            MoneyMovementId = movement.Id,
            SourceAccountId = sourceAccountId,
            TargetAccountId = targetAccountId
        }, userId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(targetAccountId, movement.AccountId);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoveMoneyMovementToAccount_ReturnsNotFound_WhenMovementIsMissing()
    {
        Guid userId = Guid.NewGuid();
        Guid sourceAccountId = Guid.NewGuid();
        Guid targetAccountId = Guid.NewGuid();
        Mock<IAccountMemberRepository> accountMemberRepository = CreateAccountMemberRepositorySequence(
        [
            CreateMember(sourceAccountId, userId, AccountAccessRole.Editor),
            CreateMember(targetAccountId, userId, AccountAccessRole.Editor)
        ]);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);
        MoveMoneyMovementToAccountUseCase useCase = new(CreateUnitOfWork(
            accountMemberRepository: accountMemberRepository,
            moneyMovementRepository: moneyMovementRepository,
            setupSaveChanges: true).Object);

        var result = await useCase.ExecuteAsync(new MoveMoneyMovementToAccountRequest
        {
            MoneyMovementId = Guid.NewGuid(),
            SourceAccountId = sourceAccountId,
            TargetAccountId = targetAccountId
        }, userId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }
}
