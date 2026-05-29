using System.Security.Claims;
using CostVision.Application.Models.Requests.Authorize;
using CostVision.Application.Models.Responses.Results;
using CostVision.Application.UseCases.Authorize.Authentication;
using CostVision.Domain.Models.Authorization;
using CostVision.Domain.Models.Enums.Authorization;
using CostVision.Web.Pages;
using CostVision.Web.UnitTests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CostVision.Web.UnitTests.Pages;

public class LoginModelTests
{
    [Fact]
    public async Task OnPostAsync_RedirectsAndSignsInUser_WhenCredentialsAreValid()
    {
        LoginRequest request = new() { Login = "user", Password = "password" };
        User user = TestUsers.Create();
        user.Roles.Add(new Role { Name = "Admin", RoleType = RoleType.Admin });

        Mock<IAuthenticateUserUseCase> authenticateUserUseCase = new(MockBehavior.Strict);
        authenticateUserUseCase
            .Setup(useCase => useCase.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<User>.Ok(user));

        FakeAuthenticationService authenticationService = new();
        LoginModel model = CreateModel(authenticateUserUseCase.Object, authenticationService);
        model.Input = request;

        IActionResult result = await model.OnPostAsync(CancellationToken.None);

        RedirectToPageResult redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Index", redirect.PageName);
        Assert.NotNull(authenticationService.SignedInPrincipal);
        Assert.Equal(user.Id.ToString(), authenticationService.SignedInPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Admin", authenticationService.SignedInPrincipal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.True(authenticationService.SignedInProperties!.IsPersistent);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsPageWithError_WhenCredentialsAreInvalid()
    {
        LoginRequest request = new() { Login = "user", Password = "wrong" };

        Mock<IAuthenticateUserUseCase> authenticateUserUseCase = new(MockBehavior.Strict);
        authenticateUserUseCase
            .Setup(useCase => useCase.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<User>.Fail(400, "Неверный логин или пароль."));

        LoginModel model = CreateModel(authenticateUserUseCase.Object, new FakeAuthenticationService());
        model.Input = request;

        IActionResult result = await model.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Неверный логин или пароль.", model.ErrorMessage);
    }

    private static LoginModel CreateModel(IAuthenticateUserUseCase authenticateUserUseCase, FakeAuthenticationService authenticationService)
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authenticationService)
            .BuildServiceProvider();

        return new LoginModel(authenticateUserUseCase)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services
                }
            }
        };
    }
}
