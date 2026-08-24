using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Web.UnitTests.Helpers;

internal static class TestUsers
{
    public static User Create(Guid? id = null, RoleType roleType = RoleType.User)
    {
        bool isRoleCreated = Role.TryCreate(roleType.ToString(), roleType, out Role? role, out string? error);
        if (!isRoleCreated || role == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую роль.");

        role.Id = Guid.NewGuid();

        bool isUserCreated = User.TryCreate(
            "user",
            "User",
            "password-hash",
            [role],
            new DateTime(2026, 1, 1),
            out User? user,
            out error);
        if (!isUserCreated || user == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестового пользователя.");

        user.Id = id ?? Guid.NewGuid();
        return user;
    }
}
