using CostVision.Application.Abstractions.DataBase.Repositories.Receipts;
using CostVision.Application.Abstractions.DataBase.Repositories;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.DataBase;

public class ProductRepositoryIntegrationTests(SqlServerContainerFixture fixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public async Task ProductRepository_SaveChanges_EnforcesUniqueNormalizedNameIndex()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        IProductRepository productRepository = serviceProvider.GetRequiredService<IProductRepository>();
        IUnitOfWorkScope storage = serviceProvider.GetRequiredService<IUnitOfWorkScope>();

        Assert.True(Product.TryCreate("Milk", "milk", out Product? product, out string? firstError), firstError);
        Assert.True(Product.TryCreate("Milk duplicate", "milk", out Product? duplicate, out string? secondError), secondError);
        productRepository.CreateRange([product!, duplicate!]);

        ReceiptWriteConflictException exception = await Assert.ThrowsAsync<ReceiptWriteConflictException>(() => storage.SaveChangesAsync(ct));

        Assert.IsType<DbUpdateException>(exception.InnerException);
        Assert.IsType<SqlException>(exception.GetBaseException());
    }
}
