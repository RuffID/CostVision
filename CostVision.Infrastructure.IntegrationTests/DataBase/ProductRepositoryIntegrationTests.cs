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
        IUnitOfWork unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        unitOfWork.Product.CreateRange(
        [
            new Product
            {
                Name = "Milk",
                NormalizedName = "milk"
            },
            new Product
            {
                Name = "Milk duplicate",
                NormalizedName = "milk"
            }
        ]);

        DbUpdateException exception = await Assert.ThrowsAsync<DbUpdateException>(() => unitOfWork.SaveChangesAsync(ct));

        Assert.IsType<SqlException>(exception.GetBaseException());
    }
}
