using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using Moq;
using System.Linq.Expressions;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using System.Data;

namespace CostVision.Application.UnitTests;

internal static class TestUnitOfWorkFactory
{
    public static void SetupReceiptTransaction(Mock<IUnitOfWork> work)
    {
        work.Setup(item => item.ExecuteInTransaction(It.IsAny<Func<CancellationToken, Task>>(),
            IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, IsolationLevel isolation, CancellationToken ct) => action(ct));
    }

    public static Mock<IUnitOfWork> CreateWithUserRepository(Mock<IUserRepository> userRepository, List<Guid>? existingRoleIds = null)
    {
        List<Role> roles = [];
        foreach (Guid roleId in existingRoleIds ?? [])
        {
            if (!Role.TryCreate("User", RoleType.User, out Role? role, out string? error))
            {
                throw new InvalidOperationException(error);
            }

            role!.Id = roleId;
            roles.Add(role);
        }

        Mock<IRoleRepository> roleRepository = new(MockBehavior.Strict);
        roleRepository.Setup(repository => repository.GetItemsByPredicateAsync(
            It.IsAny<Expression<Func<Role, bool>>>(), It.IsAny<int>(), It.IsAny<int?>(),
            true, null, It.IsAny<CancellationToken>()))
            .Returns((Expression<Func<Role, bool>> predicate, int skip, int? take, bool tracking,
                Func<IQueryable<Role>, IQueryable<Role>> include, CancellationToken ct) =>
                Task.FromResult(roles.Where(predicate.Compile()).ToList()));

        Mock<IUnitOfWork> unitOfWork = new(MockBehavior.Strict);
        unitOfWork.Setup(work => work.Role).Returns(roleRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.User).Returns(userRepository.Object);
        unitOfWork.Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return unitOfWork;
    }
}
