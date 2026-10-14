using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class UserManagementUnitOfWork(
    IUnitOfWorkScope scope,
    IUserRepository user,
    IRoleRepository role)
    : ScenarioUnitOfWork(scope), IUserManagementUnitOfWork
{
    public IUserRepository User { get; } = user;
    public IRoleRepository Role { get; } = role;
}
