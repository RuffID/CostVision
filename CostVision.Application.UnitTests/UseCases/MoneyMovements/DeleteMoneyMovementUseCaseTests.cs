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

public class DeleteMoneyMovementUseCaseTests : MoneyMovementUseCaseTestBase
{
    [Fact]
    public async Task DeleteMoneyMovement_DeletesMovement_WhenAccountIsEditable()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        MoneyMovement movement = CreateMovement(Guid.NewGuid(), accountId, 100, DateTime.Today, userId);
        MoneyMovementReceipt link = TestMoneyMovementFactory.CreateLink(
            movement,
            TestReceiptFactory.Create(Guid.NewGuid()),
            userId);
        Mock<IMoneyMovementRepository> moneyMovementRepository = CreateMoneyMovementRepository();
        moneyMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(movement);
        moneyMovementRepository.Setup(repository => repository.Delete(movement));
        Mock<IMoneyMovementReceiptRepository> linkRepository = CreateMoneyMovementReceiptRepository();
        linkRepository.Setup(repository => repository.DeleteRange(It.IsAny<IEnumerable<MoneyMovementReceipt>>()));
        Mock<IUnitOfWork> unitOfWork = CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: moneyMovementRepository,
            moneyMovementReceiptRepository: linkRepository,
            setupTransaction: true);
        DeleteMoneyMovementUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(new DeleteMoneyMovementRequest { MoneyMovementId = movement.Id, AccountId = accountId }, userId, CancellationToken.None);

        Assert.True(result.Success);
        linkRepository.Verify(repository => repository.DeleteRange(It.Is<IEnumerable<MoneyMovementReceipt>>(links => links.Single() == link)), Times.Once);
        moneyMovementRepository.Verify(repository => repository.Delete(movement), Times.Once);
        unitOfWork.Verify(unitOfWork => unitOfWork.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMoneyMovement_ReturnsError_WhenMovementIsMissingOrAccessDenied()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Mock<IMoneyMovementRepository> missingMovementRepository = CreateMoneyMovementRepository();
        missingMovementRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<MoneyMovement, bool>>>(),
                false,
                It.IsAny<Func<IQueryable<MoneyMovement>, IQueryable<MoneyMovement>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MoneyMovement?)null);
        DeleteMoneyMovementUseCase missingMovementUseCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Editor)]),
            moneyMovementRepository: missingMovementRepository,
            moneyMovementReceiptRepository: CreateMoneyMovementReceiptRepository(),
            setupTransaction: true).Object);
        DeleteMoneyMovementUseCase forbiddenUseCase = new(CreateUnitOfWork(
            accountMemberRepository: CreateAccountMemberRepositorySequence([CreateMember(accountId, userId, AccountAccessRole.Viewer)]),
            moneyMovementRepository: CreateMoneyMovementRepository()).Object);

        var missingResult = await missingMovementUseCase.ExecuteAsync(new DeleteMoneyMovementRequest { MoneyMovementId = Guid.NewGuid(), AccountId = accountId }, userId, CancellationToken.None);
        var forbiddenResult = await forbiddenUseCase.ExecuteAsync(new DeleteMoneyMovementRequest { MoneyMovementId = Guid.NewGuid(), AccountId = accountId }, userId, CancellationToken.None);

        Assert.Equal(ServiceErrorType.NotFound, missingResult.Error?.Type);
        Assert.Equal(ServiceErrorType.Forbidden, forbiddenResult.Error?.Type);
    }
}
