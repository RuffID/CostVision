using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Infrastructure.DataBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CostVision.Infrastructure.UnitTests.DataBase;

public class UserModelConfigurationTests
{
    [Fact]
    public void ProtectedAuthorizationNavigations_UseBackingFields()
    {
        using ApplicationContext context = CreateContext();

        IEntityType userEntity = context.Model.FindEntityType(typeof(User))
            ?? throw new InvalidOperationException("Конфигурация пользователя не найдена.");
        INavigation userRolesNavigation = userEntity.FindNavigation(nameof(User.UserRoles))
            ?? throw new InvalidOperationException("Навигация ролей пользователя не найдена.");
        ISkipNavigation rolesNavigation = userEntity.FindSkipNavigation(nameof(User.Roles))
            ?? throw new InvalidOperationException("Навигация справочника ролей пользователя не найдена.");

        IEntityType roleEntity = context.Model.FindEntityType(typeof(Role))
            ?? throw new InvalidOperationException("Конфигурация роли не найдена.");
        INavigation roleLinksNavigation = roleEntity.FindNavigation(nameof(Role.UserRoles))
            ?? throw new InvalidOperationException("Навигация связей роли не найдена.");
        ISkipNavigation roleUsersNavigation = roleEntity.FindSkipNavigation(nameof(Role.Users))
            ?? throw new InvalidOperationException("Навигация пользователей роли не найдена.");

        IEntityType userRoleEntity = context.Model.FindEntityType(typeof(UserRole))
            ?? throw new InvalidOperationException("Конфигурация связи пользователя с ролью не найдена.");
        INavigation userNavigation = userRoleEntity.FindNavigation(nameof(UserRole.User))
            ?? throw new InvalidOperationException("Навигация пользователя связи не найдена.");
        INavigation roleNavigation = userRoleEntity.FindNavigation(nameof(UserRole.Role))
            ?? throw new InvalidOperationException("Навигация роли связи не найдена.");

        Assert.Equal("_userRoles", userRolesNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, userRolesNavigation.GetPropertyAccessMode());
        Assert.Equal("_roles", rolesNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, rolesNavigation.GetPropertyAccessMode());
        Assert.Equal("_userRoles", roleLinksNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, roleLinksNavigation.GetPropertyAccessMode());
        Assert.Equal("_users", roleUsersNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, roleUsersNavigation.GetPropertyAccessMode());
        Assert.Equal("_user", userNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, userNavigation.GetPropertyAccessMode());
        Assert.Equal("_role", roleNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, roleNavigation.GetPropertyAccessMode());

        Dictionary<string, string> userCollectionFields = new()
        {
            [nameof(User.AccountMemberships)] = "_accountMemberships",
            [nameof(User.CreatedReceipts)] = "_createdReceipts",
            [nameof(User.Categories)] = "_categories",
            [nameof(User.Accounts)] = "_accounts",
            [nameof(User.CreatedMoneyMovements)] = "_createdMoneyMovements",
            [nameof(User.PerformedMoneyMovements)] = "_performedMoneyMovements",
            [nameof(User.CreatedMoneyMovementReceiptLinks)] = "_createdMoneyMovementReceiptLinks"
        };
        foreach (KeyValuePair<string, string> expectedField in userCollectionFields)
        {
            INavigation navigation = userEntity.FindNavigation(expectedField.Key)
                ?? throw new InvalidOperationException($"Навигация {expectedField.Key} пользователя не найдена.");
            Assert.Equal(expectedField.Value, navigation.FieldInfo?.Name);
            Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());
        }
    }

    [Fact]
    public void ProtectedExpenseCategoryNavigations_UseBackingFields()
    {
        using ApplicationContext context = CreateContext();

        IEntityType categoryEntity = context.Model.FindEntityType(typeof(ExpenseCategory))
            ?? throw new InvalidOperationException("Конфигурация категории расходов не найдена.");
        INavigation userNavigation = categoryEntity.FindNavigation(nameof(ExpenseCategory.User))
            ?? throw new InvalidOperationException("Навигация владельца категории не найдена.");
        INavigation parentNavigation = categoryEntity.FindNavigation(nameof(ExpenseCategory.Parent))
            ?? throw new InvalidOperationException("Навигация родительской категории не найдена.");
        INavigation childrenNavigation = categoryEntity.FindNavigation(nameof(ExpenseCategory.Children))
            ?? throw new InvalidOperationException("Навигация дочерних категорий не найдена.");
        INavigation receiptItemsNavigation = categoryEntity.FindNavigation(nameof(ExpenseCategory.ReceiptItems))
            ?? throw new InvalidOperationException("Навигация позиций чеков не найдена.");

        Assert.Equal("_user", userNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, userNavigation.GetPropertyAccessMode());
        Assert.Equal("_parent", parentNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, parentNavigation.GetPropertyAccessMode());
        Assert.Equal("_children", childrenNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, childrenNavigation.GetPropertyAccessMode());
        Assert.Equal("_receiptItems", receiptItemsNavigation.FieldInfo?.Name);
        Assert.Equal(PropertyAccessMode.Field, receiptItemsNavigation.GetPropertyAccessMode());
    }

    [Fact]
    public void RoleAndExpenseCategoryConstraints_ArePreserved()
    {
        using ApplicationContext context = CreateContext();

        IEntityType roleEntity = context.Model.FindEntityType(typeof(Role))
            ?? throw new InvalidOperationException("Конфигурация роли не найдена.");
        IProperty roleName = roleEntity.FindProperty(nameof(Role.Name))
            ?? throw new InvalidOperationException("Свойство названия роли не найдено.");
        Assert.False(roleName.IsNullable);
        Assert.Equal(Role.MAX_NAME_LENGTH, roleName.GetMaxLength());
        Assert.Contains(roleEntity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(Role.Name)]));

        IEntityType categoryEntity = context.Model.FindEntityType(typeof(ExpenseCategory))
            ?? throw new InvalidOperationException("Конфигурация категории расходов не найдена.");
        IProperty categoryName = categoryEntity.FindProperty(nameof(ExpenseCategory.Name))
            ?? throw new InvalidOperationException("Свойство названия категории не найдено.");
        IProperty categoryDescription = categoryEntity.FindProperty(nameof(ExpenseCategory.Description))
            ?? throw new InvalidOperationException("Свойство описания категории не найдено.");
        Assert.False(categoryName.IsNullable);
        Assert.Equal(ExpenseCategory.MAX_NAME_LENGTH, categoryName.GetMaxLength());
        Assert.Equal(ExpenseCategory.MAX_DESCRIPTION_LENGTH, categoryDescription.GetMaxLength());
        Assert.Contains(categoryEntity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(ExpenseCategory.UserId), nameof(ExpenseCategory.Name)]));
    }

    private static ApplicationContext CreateContext()
    {
        DbContextOptions<ApplicationContext> options = new DbContextOptionsBuilder<ApplicationContext>()
            .UseSqlServer("Server=localhost;Database=CostVisionModelTest;User Id=test;Password=test;TrustServerCertificate=True")
            .Options;
        return new ApplicationContext(options);
    }
}
