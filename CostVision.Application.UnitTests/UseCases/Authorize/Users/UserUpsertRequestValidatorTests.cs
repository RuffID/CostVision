using System.Reflection;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Users;
using Xunit;

namespace CostVision.Application.UnitTests.UseCases.Authorize.Users;

public class UserUpsertRequestValidatorTests
{
    [Theory]
    [InlineData("", "Name", "Password1!")]
    [InlineData("login", "", "Password1!")]
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

    [Theory]
    [InlineData("Pass1!")]
    [InlineData("password1!")]
    [InlineData("Password!")]
    [InlineData("Password1")]
    [InlineData("Пароль123!")]
    [InlineData("Password 1!")]
    public void ValidateUpsertRequest_ReturnsBadRequest_WhenPasswordIsInvalid(string password)
    {
        ServiceResult<List<Guid>> result = Validate(new UserUpsertRequest
        {
            Login = "login",
            Name = "Name",
            Password = password,
            RoleIds = [Guid.NewGuid()]
        }, requirePassword: true);

        Assert.False(result.Success);
        Assert.Equal(400, result.Error?.StatusCode);
    }

    [Theory]
    [InlineData("ab", "Name", "Password1!")]
    [InlineData("login", "A", "Password1!")]
    [InlineData("login", "Name", "Password1")]
    public void ValidateUpsertRequest_ReturnsBadRequest_WhenValueIsBelowMinimumLength(string login, string name, string password)
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
    public void ValidateUpsertRequest_ReturnsBadRequest_WhenValueIsAboveMaximumLength()
    {
        ServiceResult<List<Guid>> longLoginResult = Validate(new UserUpsertRequest
        {
            Login = new string('a', 129),
            Name = "Name",
            Password = "Password1!",
            RoleIds = [Guid.NewGuid()]
        }, requirePassword: true);

        ServiceResult<List<Guid>> longNameResult = Validate(new UserUpsertRequest
        {
            Login = "login",
            Name = new string('a', 257),
            Password = "Password1!",
            RoleIds = [Guid.NewGuid()]
        }, requirePassword: true);

        ServiceResult<List<Guid>> longPasswordResult = Validate(new UserUpsertRequest
        {
            Login = "login",
            Name = "Name",
            Password = "Password1!" + new string('a', 119),
            RoleIds = [Guid.NewGuid()]
        }, requirePassword: true);

        Assert.False(longLoginResult.Success);
        Assert.False(longNameResult.Success);
        Assert.False(longPasswordResult.Success);
        Assert.Equal(400, longLoginResult.Error?.StatusCode);
        Assert.Equal(400, longNameResult.Error?.StatusCode);
        Assert.Equal(400, longPasswordResult.Error?.StatusCode);
    }

    [Fact]
    public void ValidateUpsertRequest_ReturnsDistinctNonEmptyRoleIds_WhenRequestIsValid()
    {
        Guid roleId = Guid.NewGuid();

        ServiceResult<List<Guid>> result = Validate(new UserUpsertRequest
        {
            Login = "login",
            Name = "Name",
            Password = "Password1!",
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
