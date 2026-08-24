using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class AccountModelConfigurationTests
{
    [Theory]
    [InlineData(nameof(Account.Members), "_members")]
    [InlineData(nameof(Account.ReceiptLinks), "_receiptLinks")]
    [InlineData(nameof(Account.MoneyMovements), "_moneyMovements")]
    public void CollectionNavigation_UsesBackingField(string navigationName, string fieldName)
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=localhost;Database=CostVisionModelTest;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;
        using ApplicationContext context = new(options);

        IEntityType accountEntity = context.Model.FindEntityType(typeof(Account))
            ?? throw new InvalidOperationException("Конфигурация счёта не найдена.");
        INavigation navigation = accountEntity.FindNavigation(navigationName)
            ?? throw new InvalidOperationException($"Навигация {navigationName} не найдена.");

        Assert.Equal(fieldName, navigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());
    }
}
