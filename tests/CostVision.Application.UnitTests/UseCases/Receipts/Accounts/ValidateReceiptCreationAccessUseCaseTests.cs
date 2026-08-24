using System.Linq.Expressions;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using Moq;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Receipts.Accounts;

public class ValidateReceiptCreationAccessUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenUserIsEditor()
    {
        AccountMember member = TestAccountFactory.CreateMember(Guid.NewGuid(), Guid.NewGuid(), AccountAccessRole.Editor);
        ValidateReceiptCreationAccessUseCase useCase = new(CreateUnitOfWork(member).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsForbidden_WhenUserIsViewer()
    {
        AccountMember member = TestAccountFactory.CreateMember(Guid.NewGuid(), Guid.NewGuid(), AccountAccessRole.Viewer);
        ValidateReceiptCreationAccessUseCase useCase = new(CreateUnitOfWork(member).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenMembershipDoesNotExist()
    {
        ValidateReceiptCreationAccessUseCase useCase = new(CreateUnitOfWork(null).Object);

        var result = await useCase.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.NotFound, result.Error?.Type);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_WhenAccountIdIsEmpty()
    {
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.AccountMember).Returns(accountMemberRepository.Object);
        ValidateReceiptCreationAccessUseCase useCase = new(unitOfWork.Object);

        var result = await useCase.ExecuteAsync(Guid.Empty, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorType.Validation, result.Error?.Type);
    }

    private static Mock<IUnitOfWork> CreateUnitOfWork(AccountMember? membership)
    {
        Mock<IAccountMemberRepository> accountMemberRepository = new(MockBehavior.Strict);
        accountMemberRepository
            .Setup(repository => repository.GetItemByPredicateAsync(
                It.IsAny<Expression<Func<AccountMember, bool>>>(),
                true,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.AccountMember).Returns(accountMemberRepository.Object);

        return unitOfWork;
    }
}
