using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace CostVision.Infrastructure.IntegrationTests.DataBase.Seed;

public class ReceiptGraphSeeder(SqlServerContainerFixture fixture)
{
    public async Task<TestDataIds> SeedAsync(CancellationToken ct)
    {
        await using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        ApplicationContext context = serviceProvider.GetRequiredService<ApplicationContext>();

        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();

        User user = TestDataFactory.CreateUser(userId, "integration-user");
        Account account = TestDataFactory.CreateAccount(accountId, userId, "Integration account");
        Product product = TestDataFactory.CreateProduct(productId, "Integration product");
        Receipt receipt = TestDataFactory.CreateReceipt(receiptId, userId, accountId, product);

        context.AddRange(user, account, product, receipt);
        await context.SaveChangesAsync(ct);

        return new TestDataIds(accountId, productId, receiptId);
    }
}
