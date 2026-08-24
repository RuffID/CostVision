using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Users;

public class UserTests
{
    [Fact]
    public void TryCreate_NormalizesProfileAndCreatesActiveUserWithDistinctRoles()
    {
        Guid firstRoleId = Guid.NewGuid();
        Guid secondRoleId = Guid.NewGuid();
        DateTime createdAtUtc = new(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc);

        bool success = User.TryCreate(
            " user ",
            " Test User ",
            "password-hash",
            [firstRoleId, Guid.Empty, firstRoleId, secondRoleId],
            createdAtUtc,
            out User? user,
            out string? error);

        Assert.True(success, error);
        Assert.Equal("user", user!.Login);
        Assert.Equal("Test User", user.Name);
        Assert.Equal("password-hash", user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Equal(createdAtUtc, user.CreatedAtUtc);
        Assert.Equal(2, user.UserRoles.Count);
        Assert.Contains(user.UserRoles, userRole => userRole.RoleId == firstRoleId);
        Assert.Contains(user.UserRoles, userRole => userRole.RoleId == secondRoleId);
    }

    [Fact]
    public void TryCreate_RejectsMissingRoleWithoutCreatingPartialUser()
    {
        bool success = User.TryCreate(
            "user",
            "Test User",
            "password-hash",
            Array.Empty<Guid>(),
            DateTime.UtcNow,
            out User? user,
            out string? error);

        Assert.False(success);
        Assert.Null(user);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryUpdate_DoesNotChangeStateWhenRolesAreInvalid()
    {
        Guid originalRoleId = Guid.NewGuid();
        Assert.True(User.TryCreate(
            "user",
            "Original Name",
            "original-hash",
            [originalRoleId],
            DateTime.UtcNow,
            out User? user,
            out string? error),
            error);

        bool success = user!.TryUpdate(
            "new-login",
            "New Name",
            "new-hash",
            [],
            out error);

        Assert.False(success);
        Assert.Equal("user", user.Login);
        Assert.Equal("Original Name", user.Name);
        Assert.Equal("original-hash", user.PasswordHash);
        Assert.Equal(originalRoleId, Assert.Single(user.UserRoles).RoleId);
    }

    [Fact]
    public void TryUpdate_ReplacesRolesAndKeepsPasswordWhenHashIsNotProvided()
    {
        Guid oldRoleId = Guid.NewGuid();
        Guid newRoleId = Guid.NewGuid();
        Assert.True(User.TryCreate(
            "user",
            "Original Name",
            "original-hash",
            [oldRoleId],
            DateTime.UtcNow,
            out User? user,
            out string? error),
            error);

        bool success = user!.TryUpdate(
            " new-login ",
            " New Name ",
            null,
            [newRoleId],
            out error);

        Assert.True(success, error);
        Assert.Equal("new-login", user.Login);
        Assert.Equal("New Name", user.Name);
        Assert.Equal("original-hash", user.PasswordHash);
        Assert.Equal(newRoleId, Assert.Single(user.UserRoles).RoleId);
    }

    [Fact]
    public void TryCreate_WithRoleEntitiesBuildsConsistentRoleLinks()
    {
        Assert.True(Role.TryCreate(" Admin ", RoleType.Admin, out Role? role, out string? error), error);
        role!.Id = Guid.NewGuid();

        bool success = User.TryCreate(
            "user",
            "Test User",
            "password-hash",
            [role],
            new DateTime(2026, 7, 30),
            out User? user,
            out error);

        Assert.True(success, error);
        Assert.Same(role, Assert.Single(user!.Roles));
        UserRole userRole = Assert.Single(user.UserRoles);
        Assert.Same(user, userRole.User);
        Assert.Same(role, userRole.Role);
        Assert.Equal(role.Id, userRole.RoleId);
    }

    [Fact]
    public void RoleTryUpdateDetails_DoesNotChangeStateWhenRoleTypeIsInvalid()
    {
        Assert.True(Role.TryCreate("Admin", RoleType.Admin, out Role? role, out string? error), error);

        bool success = role!.TryUpdateDetails("Changed", (RoleType)int.MaxValue, out error);

        Assert.False(success);
        Assert.Equal("Admin", role.Name);
        Assert.Equal(RoleType.Admin, role.RoleType);
    }

    [Fact]
    public void TryMarkActivity_DoesNotChangeStateWhenDateIsMissing()
    {
        User user = TestUserFactory.Create();

        bool success = user.TryMarkActivity(default, out string? error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Null(user.LastLoginAtUtc);
    }

    [Fact]
    public void PublicApi_DoesNotExposeAuthorizationStateForMutation()
    {
        Assert.True(Role.TryCreate("Admin", RoleType.Admin, out Role? role, out string? error), error);
        role!.Id = Guid.NewGuid();
        Assert.True(User.TryCreate(
            "user",
            "Test User",
            "password-hash",
            [role],
            new DateTime(2026, 7, 30),
            out User? user,
            out error),
            error);

        Assert.Null(typeof(User).GetConstructor(Type.EmptyTypes));
        Assert.Null(typeof(UserRole).GetConstructor(Type.EmptyTypes));
        Assert.Null(typeof(Role).GetConstructor(Type.EmptyTypes));
        Assert.True(typeof(User).GetProperty(nameof(User.Login))!.SetMethod!.IsPrivate);
        Assert.True(typeof(User).GetProperty(nameof(User.IsActive))!.SetMethod!.IsPrivate);
        Assert.True(typeof(UserRole).GetProperty(nameof(UserRole.UserId))!.SetMethod!.IsPrivate);
        Assert.True(typeof(UserRole).GetProperty(nameof(UserRole.RoleId))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Role).GetProperty(nameof(Role.Name))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Role).GetProperty(nameof(Role.RoleType))!.SetMethod!.IsPrivate);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Role>)user!.Roles).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<UserRole>)user!.UserRoles).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<User>)role.Users).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<UserRole>)role.UserRoles).Clear());
    }
}
