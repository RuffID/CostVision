using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using Moq;

namespace CostVision.Application.UnitTests;

internal static class TestUnitOfWorkFactory
{
    public static Mock<IUnitOfWork> CreateWithUserRepository(Mock<IUserRepository> userRepository)
    {
        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(unitOfWork => unitOfWork.User).Returns(userRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return unitOfWork;
    }
}
