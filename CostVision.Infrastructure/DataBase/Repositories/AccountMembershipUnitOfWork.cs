using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class AccountMembershipUnitOfWork(
    IUnitOfWorkScope scope,
    IAccountRepository account,
    IAccountMemberRepository accountMember,
    IUserRepository user)
    : ScenarioUnitOfWork(scope), IAccountMembershipUnitOfWork
{
    public IAccountRepository Account { get; } = account;
    public IAccountMemberRepository AccountMember { get; } = accountMember;
    public IUserRepository User { get; } = user;
}
