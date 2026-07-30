using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class ReceiptModelConfigurationTests
{
    [Theory]
    [InlineData(nameof(Receipt.Items), "_items")]
    [InlineData(nameof(Receipt.Accounts), "_accounts")]
    [InlineData(nameof(Receipt.MoneyMovementLinks), "_moneyMovementLinks")]
    public void CollectionNavigation_UsesBackingField(string navigationName, string fieldName)
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=localhost;Database=CostVisionModelTest;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;
        using ApplicationContext context = new(options);

        IEntityType receiptEntity = context.Model.FindEntityType(typeof(Receipt))
            ?? throw new InvalidOperationException("Конфигурация чека не найдена.");
        INavigation navigation = receiptEntity.FindNavigation(navigationName)
            ?? throw new InvalidOperationException($"Навигация {navigationName} не найдена.");

        Assert.Equal(fieldName, navigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());
    }

    [Theory]
    [InlineData(typeof(Store), nameof(Store.Receipts), "_receipts")]
    [InlineData(typeof(Product), nameof(Product.ReceiptItems), "_receiptItems")]
    public void ReferenceDataCollectionNavigation_UsesBackingField(
        Type entityType,
        string navigationName,
        string fieldName)
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=localhost;Database=CostVisionModelTest;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;
        using ApplicationContext context = new(options);

        IEntityType entity = context.Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException($"Конфигурация {entityType.Name} не найдена.");
        INavigation navigation = entity.FindNavigation(navigationName)
            ?? throw new InvalidOperationException($"Навигация {navigationName} не найдена.");

        Assert.Equal(fieldName, navigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());
    }
}
