using CostVision.Domain.Models.Authorization;
using CostVision.Infrastructure.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class UserModelConfigurationTests
{
    [Fact]
    public void UserRolesNavigation_UsesBackingField()
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=localhost;Database=CostVisionModelTest;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;
        using ApplicationContext context = new(options);

        IEntityType userEntity = context.Model.FindEntityType(typeof(User))
            ?? throw new InvalidOperationException("Конфигурация пользователя не найдена.");
        INavigation userRolesNavigation = userEntity.FindNavigation(nameof(User.UserRoles))
            ?? throw new InvalidOperationException("Навигация ролей пользователя не найдена.");

        Assert.Equal("_userRoles", userRolesNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, userRolesNavigation.GetPropertyAccessMode());
    }
}
