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
            IUnitOfWork createUnitOfWork = createServiceProvider.GetRequiredService<IUnitOfWork>();

            createUnitOfWork.User.Create(user);
            createUnitOfWork.Account.Create(account);
            createUnitOfWork.AccountMember.Create(member);
            await createUnitOfWork.SaveChangesAsync(ct);
        }

        await using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        IUnitOfWork unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        Account? createdAccount = await unitOfWork.Account.GetItemByIdAsync(accountId, asNoTracking: true, ct: ct);
        AccountMember? createdMember = await unitOfWork.AccountMember.GetItemByPredicateAsync(
            x => x.AccountId == accountId && x.UserId == userId,
            asNoTracking: true,
            ct: ct);

        Assert.NotNull(createdAccount);
        Assert.Equal("Repository account", createdAccount.Name);
        Assert.NotNull(createdMember);
        Assert.Equal(AccountAccessRole.Owner, createdMember.Role);

        unitOfWork.AccountMember.Delete(createdMember);
        await unitOfWork.SaveChangesAsync(ct);

        AccountMember? deletedMember = await unitOfWork.AccountMember.GetItemByPredicateAsync(
            x => x.AccountId == accountId && x.UserId == userId,
            asNoTracking: true,
            ct: ct);

        Assert.Null(deletedMember);
    }
}
