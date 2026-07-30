using CostVision.Application.Models.Requests.Receipts;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Receipts.Accounts;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Receipts;
using CostVision.Web.Pages.Settings;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages.Settings;

public class UserSettingsModelTests
{
    [Fact]
    public async Task OnGetAccountsAsync_CallsGetUserAccountsUseCase()
    {
        User currentUser = TestUsers.Create();

        Dependencies dependencies = new();
        dependencies.GetUserAccountsUseCase
            .Setup(useCase => useCase.ExecuteAsync(currentUser.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserAccountViewModel { Id = Guid.NewGuid(), Name = "Main" }]);

        UserSettingsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = Assert.IsType<JsonResult>(await model.OnGetAccountsAsync(true, CancellationToken.None));

        Assert.Single(JsonResultAssert.Data<List<UserAccountViewModel>>(json));
    }

    [Fact]
    public async Task OnPostCreateAccountAsync_CallsCreateUseCase()
    {
        User currentUser = TestUsers.Create();
        CreateAccountRequest request = new() { Name = "Main" };
        Account account = new() { Id = Guid.NewGuid(), Name = "Main" };

        Dependencies dependencies = new();
        dependencies.CreateAccountUseCase
            .Setup(useCase => useCase.ExecuteAsync(currentUser.Id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Account>.Ok(account));

        UserSettingsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = Assert.IsType<JsonResult>(await model.OnPostCreateAccountAsync(request, CancellationToken.None));

        UserAccountViewModel data = JsonResultAssert.Data<UserAccountViewModel>(json);
        Assert.Equal(account.Id, data.Id);
    }

    [Fact]
    public async Task OnPostUpdateAccountAsync_CallsUpdateUseCase()
    {
        User currentUser = TestUsers.Create();
        UpdateAccountRequest request = new()
        {
            AccountId = Guid.NewGuid(),
            Name = "Main",
            Description = "Description",
            ColorHex = "#FFFFFF",
            IsActive = true
        };

        Dependencies dependencies = new();
        dependencies.UpdateAccountUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                currentUser.Id,
                request,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<Account>.Ok(new Account { Id = request.AccountId, Name = request.Name }));

        UserSettingsModel model = dependencies.CreateModel(currentUser);

        JsonResult json = Assert.IsType<JsonResult>(await model.OnPostUpdateAccountAsync(request, CancellationToken.None));

        Assert.Equal(request.AccountId, JsonResultAssert.Data<UserAccountViewModel>(json).Id);
        dependencies.UpdateAccountUseCase.VerifyAll();
    }

    private sealed class Dependencies
    {
        public Mock<IGetUserAccountsUseCase> GetUserAccountsUseCase { get; } = new(MockBehavior.Strict);
        public Mock<ICreateAccountUseCase> CreateAccountUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IUpdateAccountUseCase> UpdateAccountUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IGetAccountShareUsersUseCase> GetAccountShareUsersUseCase { get; } = new(MockBehavior.Strict);
        public Mock<IUpdateAccountMembersUseCase> UpdateAccountMembersUseCase { get; } = new(MockBehavior.Strict);

        public UserSettingsModel CreateModel(User currentUser)
        {
            return new UserSettingsModel(
                GetUserAccountsUseCase.Object,
                CreateAccountUseCase.Object,
                UpdateAccountUseCase.Object,
                GetAccountShareUsersUseCase.Object,
                UpdateAccountMembersUseCase.Object)
            {
                CurrentUser = currentUser
            };
        }
    }
}
