using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.IntegrationTests.DataBase.Seed;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.DataBase;

public class ReceiptCascadeDeleteIntegrationTests(SqlServerContainerFixture fixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task Receipt_Delete_CascadesReceiptItemsAndReceiptAccounts()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        ReceiptGraphSeeder seeder = new(fixture);
        TestDataIds ids = await seeder.SeedAsync(ct);

        await using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        ApplicationContext context = serviceProvider.GetRequiredService<ApplicationContext>();
        IUnitOfWork unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        Receipt receipt = await unitOfWork.Receipt.GetItemByPredicateAsync(x => x.Id == ids.ReceiptId, ct: ct)
            ?? throw new InvalidOperationException("Seeded receipt was not found.");

        unitOfWork.Receipt.Delete(receipt);
        await unitOfWork.SaveChangesAsync(ct);

        Assert.False(await context.Set<ReceiptItem>().AnyAsync(x => x.ReceiptId == ids.ReceiptId, ct));
        Assert.False(await context.Set<ReceiptAccount>().AnyAsync(x => x.ReceiptId == ids.ReceiptId, ct));
        Assert.True(await context.Set<Product>().AnyAsync(x => x.Id == ids.ProductId, ct));
        Assert.True(await context.Set<Account>().AnyAsync(x => x.Id == ids.AccountId, ct));
    }
}
