using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;

namespace CostVision.Application.Abstractions.DataBase.Repositories;

/// <summary>
/// Репозитории атомарного сценария; сохранение и отказ принадлежат общему scope.
/// </summary>
public interface IAccountMembershipUnitOfWork : IUnitOfWorkScope
{
    IAccountRepository Account { get; }
    IAccountMemberRepository AccountMember { get; }
    IUserRepository User { get; }
}
