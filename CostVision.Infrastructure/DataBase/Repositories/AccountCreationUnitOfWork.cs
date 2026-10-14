using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;

namespace CostVision.Infrastructure.DataBase.Repositories;

public class AccountCreationUnitOfWork(
    IUnitOfWorkScope scope,
    IAccountRepository account,
    IAccountMemberRepository accountMember)
    : ScenarioUnitOfWork(scope), IAccountCreationUnitOfWork
{
    public IAccountRepository Account { get; } = account;
    public IAccountMemberRepository AccountMember { get; } = accountMember;
}
