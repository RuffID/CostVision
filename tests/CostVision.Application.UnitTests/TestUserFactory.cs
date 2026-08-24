using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;

namespace CostVision.Application.UnitTests;

internal static class TestUserFactory
{
    public static User Create(
        Guid? id = null,
        string login = "user",
        string name = "User",
        bool isActive = true,
        string passwordHash = "password-hash",
        RoleType roleType = RoleType.User)
    {
        bool isRoleCreated = Role.TryCreate(roleType.ToString(), roleType, out Role? role, out string? error);
        if (!isRoleCreated || role == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестовую роль.");

        role.Id = Guid.NewGuid();

        bool isUserCreated = User.TryCreate(
            login,
            name,
            passwordHash,
            [role],
            new DateTime(2026, 1, 1),
            out User? user,
            out error);
        if (!isUserCreated || user == null)
            throw new InvalidOperationException(error ?? "Не удалось создать тестового пользователя.");

        user.Id = id ?? Guid.NewGuid();
        if (!isActive)
            user.Deactivate();

        return user;
    }
}
