using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using CostVision.Infrastructure.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CostVision.Infrastructure.IntegrationTests.DataBase;

public class ModelConfigurationIntegrationTests(SqlServerContainerFixture fixture) : IClassFixture<SqlServerContainerFixture>
{
    [Fact]
    public void ApplicationContext_ModelConfiguration_MatchesRequiredFieldsMaxLengthsAndIndexes()
    {
        using ServiceProvider serviceProvider = fixture.CreateServiceProvider();
        ApplicationContext context = serviceProvider.GetRequiredService<ApplicationContext>();
        IModel model = context.Model;

        AssertProperty<Product>(model, nameof(Product.Name), isNullable: false, maxLength: 500);
        AssertProperty<Product>(model, nameof(Product.NormalizedName), isNullable: false, maxLength: 500);
        AssertProperty<Product>(model, nameof(Product.AdaptiveName), isNullable: true, maxLength: 500);
        AssertIndex<Product>(model, isUnique: true, nameof(Product.NormalizedName));

        AssertProperty<Account>(model, nameof(Account.Name), isNullable: false, maxLength: 128);
        AssertProperty<Account>(model, nameof(Account.Description), isNullable: true, maxLength: 512);
        AssertProperty<Account>(model, nameof(Account.ColorHex), isNullable: false, maxLength: 7);

        AssertProperty<Receipt>(model, nameof(Receipt.FiscalDriveNumber), isNullable: false, maxLength: 32);
        AssertProperty<Receipt>(model, nameof(Receipt.FiscalDocumentNumber), isNullable: false, maxLength: 32);
        AssertProperty<Receipt>(model, nameof(Receipt.FiscalSign), isNullable: false, maxLength: 32);
        AssertProperty<Receipt>(model, nameof(Receipt.TotalSum), isNullable: false, maxLength: null);
        AssertIndex<Receipt>(model, isUnique: false,
            nameof(Receipt.FiscalDriveNumber),
            nameof(Receipt.FiscalDocumentNumber),
            nameof(Receipt.FiscalSign));
    }

    private static void AssertProperty<TEntity>(
        IModel model,
        string propertyName,
        bool isNullable,
        int? maxLength)
    {
        IEntityType entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} was not found.");

        IProperty property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException($"Property {typeof(TEntity).Name}.{propertyName} was not found.");

        Assert.Equal(isNullable, property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static void AssertIndex<TEntity>(IModel model, bool isUnique, params string[] propertyNames)
    {
        IEntityType entityType = model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} was not found.");

        IIndex? index = entityType.GetIndexes()
            .SingleOrDefault(x => x.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.NotNull(index);
        Assert.Equal(isUnique, index.IsUnique);
    }
}
