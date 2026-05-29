using CostVision.Domain.Models.Authorization;

namespace CostVision.Web.UnitTests.Helpers;

internal static class TestUsers
{
    public static User Create(Guid? id = null)
    {
        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Login = "user",
            Name = "User",
            IsActive = true
        };
    }
}
