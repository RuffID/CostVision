using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Репозитории атомарного сценария; сохранение и отказ принадлежат общему scope.
/// </summary>
public interface IUserManagementUnitOfWork : IUnitOfWorkScope
{
    IUserRepository User { get; }
    IRoleRepository Role { get; }
}
