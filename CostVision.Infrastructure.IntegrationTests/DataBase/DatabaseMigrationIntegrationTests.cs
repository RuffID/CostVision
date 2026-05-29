using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.DataBase;

public class DatabaseMigrationIntegrationTests(SqlServerContainerFixture fixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task Database_Migrate_CreatesCurrentSchema()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        ApplicationContext context = serviceProvider.GetRequiredService<ApplicationContext>();

        IReadOnlyList<string> pendingMigrations = (await context.Database.GetPendingMigrationsAsync(ct)).ToArray();

        Assert.Empty(pendingMigrations);
        Assert.True(await context.Database.CanConnectAsync(ct));
    }
}
