using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.IntegrationTests.DataBase.Seed;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.DataBase;

public class ReceiptRepositoryIntegrationTests(SqlServerContainerFixture fixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task ReceiptRepository_GetItemByPredicate_LoadsIncludedItemsProductsAndAccounts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ReceiptGraphSeeder seeder = new(fixture);
        TestDataIds ids = await seeder.SeedAsync(ct);

        await using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        IUnitOfWork unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        Receipt? receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(
            x => x.Id == ids.ReceiptId,
            asNoTracking: true,
            include: query => query
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .Include(x => x.Accounts)
                .ThenInclude(x => x.Account),
            ct: ct);

        Assert.NotNull(receipt);
        ReceiptItem item = Assert.Single(receipt.Items);
        ReceiptAccount accountLink = Assert.Single(receipt.Accounts);
        Assert.Equal(ids.ProductId, item.ProductId);
        Assert.Equal("Integration product", item.Product?.Name);
        Assert.Equal(ids.AccountId, accountLink.AccountId);
        Assert.Equal("Integration account", accountLink.Account?.Name);
    }
}
