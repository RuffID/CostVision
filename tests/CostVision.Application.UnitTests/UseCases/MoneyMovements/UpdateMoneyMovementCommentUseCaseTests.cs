using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.MoneyMovements;
using CostVision.Application.Models.Requests.MoneyMovements;
using CostVision.Application.UseCases.MoneyMovements;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.MoneyMovements;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.MoneyMovements;

public class UpdateMoneyMovementCommentUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task UpdateMoneyMovementComment_TrimsOrClearsComment()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = CreateMovement(Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .SetupSequence(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement)
            .ReturnsAsync(movement);
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence(
            [
                CreateMember(accountId, userId, AccountAccessRole.Editor),
                CreateMember(accountId, userId, AccountAccessRole.Editor)
            ]),
            moneyMovementRepository: moneyMovementRepository,
            setupSaveChanges: true);
        UpdateMoneyMovementCommentUseCase useCase = new(unitOfWork.Object);

        var updateResult = await useCase.ExecuteAsync(new UpdateMoneyMovementCommentRequest
        {
            AccountId = accountId,
            MoneyMovementId = movement.Id,
            Comment = "  new comment  "
        }, userId, CancellationToken.None);
        var clearResult = await useCase.ExecuteAsync(new UpdateMoneyMovementCommentRequest
        {
            AccountId = accountId,
            MoneyMovementId = movement.Id,
            Comment = "   "
        }, userId, CancellationToken.None);

        Assert.True(updateResult.Success);
        Assert.True(clearResult.Success);
        Assert.Null(movement.Comment);
        unitOfWork.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateMoneyMovementComment_ReturnsError_WhenCommentIsTooLongOrMovementMissing()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Mock<IMoneyMovementRepository> missingMovementRepository = CreateMoneyMovementRepository();
        missingMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);
        Mock<IMoneyMovementRepository> existingMovementRepository = CreateMoneyMovementRepository();
        existingMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMovement(Guid.NewGuid(), accountId, 100, DateTime.Today, userId));
        UpdateMoneyMovementCommentUseCase tooLongUseCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: existingMovementRepository).Object);
        UpdateMoneyMovementCommentUseCase missingUseCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: missingMovementRepository).Object);

        var tooLongResult = await tooLongUseCase.ExecuteAsync(new UpdateMoneyMovementCommentRequest
        {
            AccountId = accountId,
            MoneyMovementId = Guid.NewGuid(),
            Comment = new string('a', 1025)
        }, userId, CancellationToken.None);
        var missingResult = await missingUseCase.ExecuteAsync(new UpdateMoneyMovementCommentRequest
        {
            AccountId = accountId,
            MoneyMovementId = Guid.NewGuid(),
            Comment = "comment"
        }, userId, CancellationToken.None);

        Assert.Equal(ServiceErrorType.Validation, tooLongResult.Error?.Type);
        Assert.Equal(ServiceErrorType.NotFound, missingResult.Error?.Type);
    }
}
