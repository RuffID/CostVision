using System.Reflection;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Users;

public class UserUpsertRequestValidatorTests
{
    [Theory]
    [InlineData("", "Name", "password")]
    [InlineData("login", "", "password")]
    [InlineData("login", "Name", "")]
    public void ValidateUpsertRequest_ReturnsBadRequest_WhenRequiredFieldIsEmpty(string login, string name, string password)
    {
        ServiceResult<List<Guid>> result = Validate(new UserUpsertRequest
        {
            Login = login,
            Name = name,
            Password = password,
            RoleIds = [Guid.NewGuid()]
        }, requirePassword: true);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Fact]
    public void ValidateUpsertRequest_ReturnsDistinctNonEmptyRoleIds_WhenRequestIsValid()
    {
        Guid roleId = Guid.NewGuid();

        ServiceResult<List<Guid>> result = Validate(new UserUpsertRequest
        {
            Login = "login",
            Name = "Name",
            Password = "password",
            RoleIds = [Guid.Empty, roleId, roleId]
        }, requirePassword: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal([roleId], result.Data);
    }

    private static ServiceResult<List<Guid>> Validate(UserUpsertRequest request, bool requirePassword)
    {
        Type type = typeof(CreateUserUseCase).Assembly.GetType("CostVision.Application.UseCases.Authorize.Users.Helpers.UserUpsertRequestValidator")!;
        MethodInfo method = type.GetMethod("ValidateUpsertRequest", BindingFlags.Public | BindingFlags.Static)!;
        return (ServiceResult<List<Guid>>)method.Invoke(null, [request, requirePassword])!;
    }
}
