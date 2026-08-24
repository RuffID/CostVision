using CostVision.Domain.Models.MoneyMovements;
using CostVision.Infrastructure.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class MoneyMovementModelConfigurationTests
{
    [Fact]
    public void ReceiptLinksNavigation_UsesBackingField()
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=localhost;Database=CostVisionModelTest;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;
        using ApplicationContext context = new(options);

        IEntityType entity = context.Model.FindEntityType(typeof(MoneyMovement))
            ?? throw new InvalidOperationException("Конфигурация операции не найдена.");
        INavigation navigation = entity.FindNavigation(nameof(MoneyMovement.ReceiptLinks))
            ?? throw new InvalidOperationException("Навигация связей с чеками не найдена.");

        Assert.Equal("_receiptLinks", navigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());
    }
}
