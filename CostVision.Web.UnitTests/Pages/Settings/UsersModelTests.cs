using CostVision.Application.Models.Dtos.Authorization;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Roles;
using CostVision.Application.UseCases.Authorize.Users;
using CostVision.Domain.Models.Authorization;
using CostVision.Web.Pages.Settings;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages.Settings;

public class UsersModelTests
{
    [Fact]
    public async Task OnGetUserListAsync_CallsUserListUseCase()
    {
        Dependencies dependencies = new();
        dependencies.GetUserListUseCase
            .Setup(useCase => useCase.ExecuteAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<List<UserListItemDto>>.Ok([new UserListItemDto { Id = Guid.NewGuid(), Login = "user" }]));

        UsersModel model = dependencies.CreateModel();

        JsonResult json = Assert.IsType<JsonResult>(await model.OnGetUserListAsync(true, CancellationToken.None));

        Assert.Single(JsonResultAssert.Data<List<UserListItemDto>>(json));
    }

    [Fact]
    public async Task OnPostCreateAndUpdate_CallExpectedUseCases()
    {
        UserUpsertRequest createRequest = new() { Login = "new-user", Name = "New User" };
        UserUpsertRequest updateRequest = new() { Id = Guid.NewGuid(), Login = "user", Name = "User" };

        Dependencies dependencies = new();
        dependencies.CreateUserUseCase
            .Setup(useCase => useCase.ExecuteAsync(createRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());
        dependencies.UpdateUserUseCase
            .Setup(useCase => useCase.ExecuteAsync(updateRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult.Ok());

        UsersModel model = dependencies.CreateModel();

        JsonResultAssert.Success(Assert.IsType<JsonResult>(await model.OnPostCreateAsync(createRequest, CancellationToken.None)));
        JsonResultAssert.Success(Assert.IsType<JsonResult>(await model.OnPostUpdateAsync(updateRequest, CancellationToken.None)));
    }

    [Fact]
    public async Task OnPostToggleActiveAsync_CallsToggleUseCase()
    {
        Guid userId = Guid.NewGuid();

        Dependencies dependencies = new();
        dependencies.ToggleUserActiveUseCase
            .Setup(useCase => useCase.ExecuteAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));

        UsersModel model = dependencies.CreateModel();

        JsonResult json = Assert.IsType<JsonResult>(await model.OnPostToggleActiveAsync(userId, CancellationToken.None));

        JsonResultAssert.Data<bool>(json);
        dependencies.ToggleUserActiveUseCase.VerifyAll();
    }

    private sealed class Dependencies
    {
        public Mock<IGetUserListUseCase> GetUserListUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetUserUseCase> GetUserUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ICreateUserUseCase> CreateUserUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IUpdateUserUseCase> UpdateUserUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IToggleUserActiveUseCase> ToggleUserActiveUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetRoleListUseCase> GetRoleListUseCase { get; } = new(MockBehavior.Strict);

        public UsersModel CreateModel()
        {
            return new UsersModel(
                GetUserListUseCase.Object,
                GetUserUseCase.Object,
                CreateUserUseCase.Object,
                UpdateUserUseCase.Object,
                ToggleUserActiveUseCase.Object,
                GetRoleListUseCase.Object)
            {
                CurrentUser = TestUsers.Create()
            };
        }
    }
}
