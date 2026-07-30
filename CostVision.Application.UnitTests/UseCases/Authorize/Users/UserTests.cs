using CostVision.Domain.Models.Authorization;
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
            [],
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
}
