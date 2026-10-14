using CostVision.Application.Abstractions.DataBase.Repositories.Authorization;
using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.IntegrationTests.DataBase.Seed;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.DataBase;

public class RepositoryCrudIntegrationTests(SqlServerContainerFixture fixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task Repositories_CreateReadAndDelete_UseSqlServerDatabase()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        User user = TestDataFactory.CreateUser(userId, "repository-user");
        Account account = TestDataFactory.CreateAccount(accountId, userId, "Repository account");
        AccountMember member = account.Members.Single(item => item.Role == AccountAccessRole.Owner);

        await using (ServiceProvider createServiceProvider = fixture.CreateServiceProvider())
        {
            IUnitOfWorkScope createScope = createServiceProvider.GetRequiredService<IUnitOfWorkScope>();

            createServiceProvider.GetRequiredService<IUserRepository>().Create(user);
            createServiceProvider.GetRequiredService<IAccountRepository>().Create(account);
            createServiceProvider.GetRequiredService<IAccountMemberRepository>().Create(member);
            await createScope.SaveChangesAsync(ct);
        }

        await using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        IAccountRepository accountRepository = serviceProvider.GetRequiredService<IAccountRepository>();
        IAccountMemberRepository accountMemberRepository = serviceProvider.GetRequiredService<IAccountMemberRepository>();
        IUnitOfWorkScope storage = serviceProvider.GetRequiredService<IUnitOfWorkScope>();

        Account? createdAccount = await accountRepository.GetItemByIdAsync(accountId, asNoTracking: true, ct: ct);
        AccountMember? createdMember = await accountMemberRepository.GetItemByPredicateAsync(
            x => x.AccountId == accountId && x.UserId == userId,
            asNoTracking: true,
            ct: ct);

        Assert.NotNull(createdAccount);
        Assert.Equal("Repository account", createdAccount.Name);
        Assert.NotNull(createdMember);
        Assert.Equal(AccountAccessRole.Owner, createdMember.Role);

        accountMemberRepository.Delete(createdMember);
        await storage.SaveChangesAsync(ct);

        AccountMember? deletedMember = await accountMemberRepository.GetItemByPredicateAsync(
            x => x.AccountId == accountId && x.UserId == userId,
            asNoTracking: true,
            ct: ct);

        Assert.Null(deletedMember);
    }
}
